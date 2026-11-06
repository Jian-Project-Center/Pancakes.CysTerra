using System;
using System.Globalization;
using System.Text.Json;

namespace Pancakes.CysTerra {
	/// <summary>
	/// 解析 PancakesAPI /gq（官方旧 TCP ObjectStream 已停，字段映射尽量对齐 GlobalQuakeReport）。
	/// </summary>
	static class LineParser {
		public static LineEvent? Parse(JsonElement root) {
			if (root.ValueKind != JsonValueKind.Object) {
				return null;
			}

			var type = Text(root, "type");
			if (type is "heartbeat" or "pong" or "error" or "ping") {
				return null;
			}

			var action = Text(root, "action");
			JsonElement data;
			if (root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object) {
				data = payload;
				if (type != null && type != "earthquake") {
					return null;
				}
			}
			else if (root.TryGetProperty("Data", out var legacy) && legacy.ValueKind == JsonValueKind.Object) {
				data = legacy;
			}
			else if (root.TryGetProperty("data", out var camel) && camel.ValueKind == JsonValueKind.Object) {
				data = camel;
			}
			else {
				data = root;
			}

			var status = Text(data, "status") ?? Text(root, "status");
			var cancel = string.Equals(action, "cancelled", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(action, "canceled", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(status, "canceled", StringComparison.OrdinalIgnoreCase)
				|| Flag(data, "isCancel", "cancelled", "canceled");
			var final = string.Equals(action, "archived", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(status, "archived", StringComparison.OrdinalIgnoreCase)
				|| Flag(data, "isFinal");

			var mag = Num(data, "magnitude");
			var lat = Num(data, "latitude");
			var lon = Num(data, "longitude");
			var depth = Num(data, "depth", "depthKm");
			var place = Text(data, "region", "place", "placeName");
			var serial = Revision(data);
			if (serial == null && !cancel && !final) {
				serial = 1;
			}
			var id = Text(data, "id", "eventId", "EventID", "uuid")
				?? string.Join("|", place ?? "", Fmt(lat), Fmt(lon), Fmt(mag));
			var origin = Time(data, "originTimeMs", "originTime", "originTimeIso", "time", "startAt", "origin");
			var lastUpdated = Time(data, "lastUpdate", "lastUpdated", "lastUpdateMs", "updatedAt", "finalUpdateMillis")
				?? Time(root, "timestampMs", "timestamp")
				?? origin;
			var intensity = Intensity(data);
			var intensityMmi = MmiIntensity.TryParse(intensity);
			ParseQuality(data, out var qualityLevel, out var errDepth);

			if (!cancel && (mag == null || lat == null || lon == null || depth == null || origin == null)) {
				return null;
			}

			var fp = string.Join("|", id, serial, cancel, final, Fmt(mag), Fmt(depth), Fmt(lat), Fmt(lon), place, intensity,
				qualityLevel, lastUpdated?.Ticks);
			return new LineEvent {
				Agency = "gq",
				UnitId = id,
				Serial = serial,
				IsCancel = cancel,
				IsFinal = final,
				Place = place,
				Magnitude = mag,
				DepthKm = depth,
				Latitude = lat,
				Longitude = lon,
				OriginUtc = origin,
				LastUpdatedUtc = lastUpdated,
				Intensity = intensity,
				IntensityMmi = intensityMmi,
				QualityLevel = qualityLevel,
				QualityErrDepth = errDepth,
				Fingerprint = fp,
			};
		}

		/// <summary>对齐官方：显式 qualityLevel/qualityID，或由误差字段推等级。</summary>
		static void ParseQuality(JsonElement data, out int? level, out float? errDepth) {
			level = null;
			errDepth = null;
			JsonElement q = default;
			var hasQ = data.TryGetProperty("quality", out q) && q.ValueKind == JsonValueKind.Object;
			if (!hasQ) {
				level = LevelOrNull(data, "qualityLevel", "qualityID", "qualityId");
				return;
			}

			level = LevelOrNull(q, "qualityLevel", "qualityID", "qualityId");
			errDepth = AsFloat(Num(q, "errDepth"));
			if (level != null) {
				return;
			}

			var errEW = AsFloat(Num(q, "errEW")) ?? 0f;
			var errNS = AsFloat(Num(q, "errNS")) ?? 0f;
			var errOrigin = AsFloat(Num(q, "errOrigin")) ?? 0f;
			var pct = AsFloat(Num(q, "pct")) ?? 0f;
			var stations = (int)(Num(q, "stations") ?? 0);
			var ed = errDepth ?? 0f;
			level = ComputeQualityLevel(ed, errEW, errNS, errOrigin, pct, stations);
		}

		static int ComputeQualityLevel(
			float errDepth, float errEW, float errNS, float errOrigin, float pct, int stations) {
			int d = errDepth < 8f ? 0 : errDepth < 20f ? 1 : errDepth < 40f ? 2 : errDepth < 100f ? 3 : 4;
			int ew = errEW < 5f ? 0 : errEW < 12f ? 1 : errEW < 24f ? 2 : errEW < 60f ? 3 : 4;
			int ns = errNS < 5f ? 0 : errNS < 12f ? 1 : errNS < 24f ? 2 : errNS < 60f ? 3 : 4;
			int o = errOrigin < 1.2f ? 0 : errOrigin < 3f ? 1 : errOrigin < 9f ? 2 : errOrigin < 20f ? 3 : 4;
			int p = pct >= 90f ? 0 : pct >= 80f ? 1 : pct >= 65f ? 2 : pct >= 55f ? 3 : 4;
			int s = stations >= 12 ? 0 : stations >= 10 ? 1 : stations >= 8 ? 2 : stations >= 6 ? 3 : 4;
			return Math.Max(d, Math.Max(ew, Math.Max(ns, Math.Max(o, Math.Max(p, s)))));
		}

		static int? LevelOrNull(JsonElement o, params string[] keys) {
			foreach (var k in keys) {
				if (!o.TryGetProperty(k, out var v)) {
					continue;
				}
				if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) && i >= 0 && i <= 6) {
					return i;
				}
				if (v.ValueKind == JsonValueKind.String) {
					var s = v.GetString()?.Trim();
					if (string.IsNullOrEmpty(s)) {
						continue;
					}
					if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
						&& n >= 0 && n <= 6) {
						return n;
					}
					return s.ToUpperInvariant() switch {
						"S" => 0,
						"A" => 1,
						"B" => 2,
						"C" => 3,
						"D" => 4,
						"E" => 5,
						"F" => 6,
						_ => null,
					};
				}
			}
			return null;
		}

		static float? AsFloat(double? n) => n is double d ? (float)d : null;

		static string? Text(JsonElement o, params string[] keys) {
			foreach (var k in keys) {
				if (!o.TryGetProperty(k, out var v)) {
					continue;
				}
				if (v.ValueKind == JsonValueKind.String) {
					var s = v.GetString()?.Trim();
					if (!string.IsNullOrEmpty(s)) {
						return s;
					}
				}
				if (v.ValueKind == JsonValueKind.Number) {
					return v.GetRawText();
				}
			}
			return null;
		}

		static double? Num(JsonElement o, params string[] keys) {
			foreach (var k in keys) {
				if (!o.TryGetProperty(k, out var v)) {
					continue;
				}
				if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n)) {
					return n;
				}
				if (v.ValueKind == JsonValueKind.String
					&& double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) {
					return s;
				}
			}
			return null;
		}

		static int? Int(JsonElement o, params string[] keys) {
			var n = Num(o, keys);
			if (n == null) {
				return null;
			}
			var i = (int)n.Value;
			return i > 0 ? i : null;
		}

		/// <summary>优先 revisionId；忽略像毫秒时间戳的巨大 revision。</summary>
		static int? Revision(JsonElement o) {
			var id = Int(o, "revisionId", "updates", "reportNum");
			if (id != null) {
				return id;
			}
			var rev = Num(o, "revision");
			if (rev == null || rev <= 0 || rev > 100_000) {
				return null;
			}
			return (int)rev.Value;
		}

		static string? Intensity(JsonElement o) {
			var s = Text(o, "intensity", "maxIntensity");
			if (string.IsNullOrEmpty(s) || s is "-" or "—" or "N/A" or "n/a" or "null") {
				return null;
			}
			return s;
		}

		static DateTime? Time(JsonElement o, params string[] keys) {
			foreach (var k in keys) {
				if (!o.TryGetProperty(k, out var v)) {
					continue;
				}
				if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n)) {
					var ms = n > 1e12 ? (long)n : n > 1e9 ? (long)(n * 1000) : 0L;
					if (ms > 0) {
						return DateTime.UnixEpoch.AddMilliseconds(ms);
					}
				}
				if (v.ValueKind == JsonValueKind.String
					&& DateTime.TryParse(v.GetString(), CultureInfo.InvariantCulture,
						DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)) {
					return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
				}
			}
			return null;
		}

		static bool Flag(JsonElement o, params string[] keys) {
			foreach (var k in keys) {
				if (!o.TryGetProperty(k, out var v)) {
					continue;
				}
				if (v.ValueKind == JsonValueKind.True) {
					return true;
				}
				if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) && n != 0) {
					return true;
				}
				if (v.ValueKind == JsonValueKind.String
					&& v.GetString() is "1" or "true" or "True" or "cancelled" or "canceled" or "archived") {
					return true;
				}
			}
			return false;
		}

		static string Fmt(double? n) => n?.ToString("G", CultureInfo.InvariantCulture) ?? "";
	}
}
