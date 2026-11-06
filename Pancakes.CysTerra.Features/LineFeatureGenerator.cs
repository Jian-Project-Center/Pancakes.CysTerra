using Cryville.Common.Compat;
using Cryville.EEW.Features;
using Pancakes.CysTerra;
using Cryville.Measure;
using System;
using System.Globalization;
using static Cryville.EEW.TagTypeKeys;
using Cryville.EEW;

namespace Pancakes.CysTerra.Features {
	/// <summary>
	/// 对齐官方 GlobalQuakeReportFeatureGenerator（不写 Name；Ongoing=!归档；质量误差写深度不确定度）。
	/// </summary>
	public sealed class LineFeatureGenerator : IGenerator<LineEvent, Feature?> {
		static readonly Unit Kilometre = Units.Metre.WithPrefix(MetricPrefixes.Kilo);

		public Feature? Generate(LineEvent e, ref CultureInfo culture) {
			ThrowHelper.ThrowIfNull(e);
			if (e.IsCancel) {
				return null;
			}

			var feature = new Feature {
				{ Is, Earthquake },
				{ Ongoing, !e.IsFinal },
			};
			if (e.OriginUtc is DateTime origin) {
				feature[Time] = new DateTimeOffset(DateTime.SpecifyKind(origin, DateTimeKind.Utc));
			}
			if (e.LastUpdatedUtc is DateTime updated) {
				feature[TimeModified] = new DateTimeOffset(DateTime.SpecifyKind(updated, DateTimeKind.Utc));
			}
			if (e.Magnitude is double mag) {
				feature[Magnitude] = new Quantity(mag, Units.Dimensionless);
			}
			if (e.Latitude is double lat && e.Longitude is double lon) {
				var at = new Feature(new Point(lon, lat)) { { Is, Hypocenter } };
				if (e.DepthKm is double depth) {
					if (e.QualityErrDepth is float err) {
						at[HypocenterDepth] = new QuantityUnc((float)depth, err, Kilometre);
					}
					else {
						at[HypocenterDepth] = new Quantity(depth, Kilometre);
					}
				}
				feature[At] = at;
			}
			return feature;
		}
	}
}
