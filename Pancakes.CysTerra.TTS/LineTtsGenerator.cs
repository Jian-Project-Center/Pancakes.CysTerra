using Cryville.Common.Compat;
using Pancakes.CysTerra;
using Cryville.EEW.TTS;
using System.Globalization;
using System.Text;
using Cryville.EEW;

namespace Pancakes.CysTerra.TTS {
	public sealed class LineTtsGenerator : IContextedGenerator<LineEvent, ITTSMessageGeneratorContext, TTSEntry?> {
		public TTSEntry? Generate(LineEvent e, ITTSMessageGeneratorContext? context, ref CultureInfo culture) {
			ThrowHelper.ThrowIfNull(e);
			context ??= EmptyTTSMessageGeneratorContext.Instance;

			using var lres = new LocalizedResource("", ref culture);
			var res = lres.RootMessageStringSet;

			var spoken = new StringBuilder();
			spoken.Append(res.GetStringRequired("TtsLead"));
			spoken.Append(res.GetStringRequired(TitleKey(e)));
			var place = ResolvePlace(e, context, ref culture);
			if (!string.IsNullOrEmpty(place)) {
				spoken.Append(string.Format(culture, res.GetStringRequired("TtsPlace"), place));
			}
			if (e.Magnitude is double mag) {
				spoken.Append(string.Format(culture, res.GetStringRequired("TtsMagnitude"), mag.ToString("F1", culture)));
			}
			if (e.DepthKm is double depth) {
				spoken.Append(string.Format(culture, res.GetStringRequired("TtsDepth"), depth.ToString("0", culture)));
			}
			if (!string.IsNullOrEmpty(e.Intensity)) {
				spoken.Append(string.Format(culture, res.GetStringRequired("TtsIntensity"), e.Intensity));
			}

			var priority = e.IsCancel ? -50 : -100;
			// 标题键对齐官方 GlobalQuake：检出/归档/取消；TtsLead 空
			var repeatTitle = res.GetStringRequired("TtsRepeatTitle");
			return new TTSEntry(culture, repeatTitle, spoken.ToString(), priority, SoundFor(e));
		}

		static string? ResolvePlace(LineEvent e, ITTSMessageGeneratorContext context, ref CultureInfo culture) {
			if (e.Latitude is double lat && e.Longitude is double lon) {
				var target = culture;
				// 对齐官方报告：localCulture = null
				if (context.NameLocation(lat, lon, null, ref target, out var named)
					&& !string.IsNullOrEmpty(named)) {
					return named;
				}
			}
			return e.Place;
		}

		static string? SoundFor(LineEvent e) {
			if (e.IsCancel) {
				return "eew_update_cancel";
			}
			if (e.IsFinal) {
				return "eew_update_final";
			}
			if (e.Serial is > 1) {
				return "eew_update";
			}
			return IntensitySound(e.IntensityMmi);
		}

		static string IntensitySound(float? mmi) {
			if (mmi is null) {
				return "eew_2";
			}
			var n = mmi.Value;
			if (n >= 9) {
				return "eew_5";
			}
			if (n >= 7) {
				return "eew_4";
			}
			if (n >= 5) {
				return "eew_3";
			}
			if (n >= 3) {
				return "eew_2";
			}
			return "eew_1";
		}

		static string TitleKey(LineEvent e) {
			if (e.IsCancel) {
				return "TitleCanceled";
			}
			if (e.IsFinal) {
				return "TitleArchived";
			}
			// 官方进行中一律 Title，无「更新」标题
			return "Title";
		}
	}
}
