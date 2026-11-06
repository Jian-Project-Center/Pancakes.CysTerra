using System;

namespace Pancakes.CysTerra {
	/// <summary>
	/// PancakesAPI /gq 事件；字段语义对齐官方 Cryville.EEW.GlobalQuake 的 GlobalQuakeReport。
	/// </summary>
	public sealed class LineEvent {
		public string Agency { get; init; } = "gq";
		public string UnitId { get; init; } = "";
		/// <summary>进行中报数（&gt;0）；终报/取消不依赖此值展示 Serial。</summary>
		public int? Serial { get; init; }
		public bool IsCancel { get; init; }
		/// <summary>归档终报（对齐官方 IsArchive / revisionID==0）。</summary>
		public bool IsFinal { get; init; }
		public string? Place { get; init; }
		public double? Magnitude { get; init; }
		public double? DepthKm { get; init; }
		public double? Latitude { get; init; }
		public double? Longitude { get; init; }
		public DateTime? OriginUtc { get; init; }
		/// <summary>上游 lastUpdate；对齐官方 LastUpdatedTime → UtcIssueTime。</summary>
		public DateTime? LastUpdatedUtc { get; init; }
		/// <summary>PancakesAPI 额外字段：MMI 展示原文。</summary>
		public string? Intensity { get; init; }
		/// <summary>IntensityMMI 数值 1–12。</summary>
		public float? IntensityMmi { get; init; }
		/// <summary>质量等级 0=S … 6=F；对齐官方 QualityLevel。</summary>
		public int? QualityLevel { get; init; }
		/// <summary>深度误差 km（有则 Features 可写不确定度）。</summary>
		public float? QualityErrDepth { get; init; }
		public string Fingerprint { get; init; } = "";
	}
}
