using Cryville.Common.Compat;
using Cryville.EEW.Report;
using System;
using System.Globalization;
using static Cryville.EEW.TagTypeKeys;
using Cryville.EEW;

namespace Pancakes.CysTerra {
	/// <summary>
	/// 报告生成逻辑对齐官方 Cryville.EEW.GlobalQuake.GlobalQuakeReportGenerator
	///（数据源改为 PancakesAPI；官方旧 TCP 服务已停）。
	/// </summary>
	public sealed class LineReportGenerator : IContextedGenerator<LineEvent, IReportGeneratorContext, ReportModel> {
		// 与官方 GlobalQuakeReportGenerator._timeoutTable 一致
		static readonly int[] TimeoutTable = [
			3, 3, // M0
			3, 3, // M1
			3, 3, // M2
			5, 6, // M3
			8, 16, // M4
			30, 30, // M5
			30, 40, // M6
			40, 40, // M7+
		];

		static readonly TagTypeKey TagQualityGlobalQuake = "Quality:GlobalQuake";

		public ReportModel Generate(LineEvent e, IReportGeneratorContext? context, ref CultureInfo culture) {
			ThrowHelper.ThrowIfNull(e);
			context ??= EmptyReportGeneratorContext.Instance;
			using var lres = new LocalizedResource("", ref culture);
			var res = lres.RootMessageStringSet;

			// Source 出现在预警条：保持与官方一致的 GlobalQuake
			var result = new ReportModel {
				Source = res.GetStringRequired("AuthorityName"),
			};
			result.GroupKeys.Add(new UnitKey(e.UnitId));
			result.RevisionKey = new RevKey(e.Serial ?? 0, e.IsCancel, e.IsFinal);

			// 官方：revisionID==-1 只出取消标题后返回
			if (e.IsCancel) {
				result.Title = res.GetStringRequired("TitleCanceled");
				return result;
			}

			// 官方：NameLocationTo(..., localCulture: null)；失败再 region + specificity 3
			if (e.Latitude is double nameLat && e.Longitude is double nameLon) {
				if (!context.NameLocationTo(result, nameLat, nameLon, null, culture)) {
					if (!string.IsNullOrEmpty(e.Place)) {
						result.Location = e.Place;
						result.LocationSpecificity = 3;
					}
				}
			}
			else if (!string.IsNullOrEmpty(e.Place)) {
				result.Location = e.Place;
				result.LocationSpecificity = 3;
			}

			if (e.OriginUtc is DateTime origin) {
				result.Time = new DateTimeOffset(DateTime.SpecifyKind(origin, DateTimeKind.Utc));
			}
			result.TimeZone = TimeZoneInfo.Utc;

			if (e.Latitude is double lat && e.Longitude is double lon && e.Magnitude is double mag
				&& e.DepthKm is double depth && e.OriginUtc is DateTime ht) {
				result.GroupKeys.Add(new HypocenterGroupKey(
					(float)lat, (float)lon, DateTime.SpecifyKind(ht, DateTimeKind.Utc), (float)mag, (float)depth));
			}

			result.UtcIssueTime = e.LastUpdatedUtc ?? DateTime.UtcNow;

			if (e.IsFinal) {
				result.Title = res.GetStringRequired("TitleArchived");
				// 官方归档报不设 InvalidatedTime
			}
			else {
				result.Title = res.GetStringRequired("Title");
				if (e.OriginUtc is DateTime o && e.Magnitude is double m0 && e.DepthKm is double d0) {
					result.InvalidatedTime = new DateTimeOffset(
						DateTime.SpecifyKind(o, DateTimeKind.Utc)
						+ TimeSpan.FromMinutes(1.5 * TimeoutMinutes(m0, d0)));
				}
			}

			// 与 Jian 一致：烈度先加（列表左侧色块），再震级/深度；AccuracyOrder 同档，避免
			// 烈度用 10、震级深度用 90 时右侧把「非官方」震级/深度藏掉。
			if (e.IntensityMmi is float maxIntensity) {
				result.Properties.Add(RomanNumerals.CreateRomanIntensityProperty(
					TagTypeKeys.IntensityMMI, res.GetStringRequired("PropertyMaxIntensity"),
					maxIntensity, culture, context.SeverityScheme, 30));
			}
			if (e.Magnitude is double m) {
				result.Properties.Add(new(
					Magnitude, res.GetStringRequired("PropertyMagnitude"), m.ToString("F1", culture),
					context.SeverityScheme, m) { AccuracyOrder = 30 });
			}
			if (e.DepthKm is double d) {
				result.Properties.Add(new(
					HypocenterDepth, res.GetStringRequired("PropertyDepth"),
					string.Format(culture, res.GetStringRequired("PropertyDepthValue"), d.ToString("F1", culture)),
					context.SeverityScheme, d) { AccuracyOrder = 30 });
			}
			if (e.QualityLevel is int qLevel) {
				var text = qLevel switch {
					0 => "S",
					1 => "A",
					2 => "B",
					3 => "C",
					4 => "D",
					5 => "E",
					6 => "F",
					_ => "?",
				};
				var qset = res.GetStringSetRequired("PropertyQualityValue");
				result.Properties.Add(new ReportProperty(
					TagQualityGlobalQuake, res.GetStringRequired("PropertyQuality"),
					qset.GetStringOrDefault(text, ""), context.SeverityScheme, text) {
					AccuracyOrder = 30,
				});
			}
			return result;
		}

		static int TimeoutMinutes(double magnitude, double depthKm) {
			var idx = (int)Math.Clamp(
				(magnitude + Math.Log10(depthKm + 160) - Math.Log10(160)) * 2,
				0, TimeoutTable.Length - 1);
			return TimeoutTable[idx];
		}

		sealed record UnitKey(string Id) : IReportUnitKey { }

		/// <summary>对齐官方 ReportRevisionKey：取消=-1，终报=0，进行中=正报数。</summary>
		sealed record RevKey(int Number, bool Cancel, bool Final) : IReportRevisionKey {
			int Code => Cancel ? -1 : Final ? 0 : Number;

			public int? Serial => Code > 0 ? Code : null;
			public bool IsCancellation => Cancel;
			public bool IsFinalRevision => Final;
			public bool IsComparableWith(IReportRevisionKey obj) => obj is RevKey;

			public int CompareTo(IReportRevisionKey? obj) {
				if (obj is not RevKey other) {
					throw new ArgumentException("Mismatched revision key type.");
				}
				var a = Code;
				var b = other.Code;
				if (a == -1) {
					return b == -1 ? 0 : 1;
				}
				if (b == -1) {
					return -1;
				}
				if (a == 0) {
					return b == 0 ? 0 : 1;
				}
				if (b == 0) {
					return -1;
				}
				return a.CompareTo(b);
			}
		}
	}
}
