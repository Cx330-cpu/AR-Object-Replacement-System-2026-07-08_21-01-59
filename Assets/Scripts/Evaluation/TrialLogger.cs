using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ARObjectReplacement.Evaluation
{
    public sealed class TrialFrameRecord
    {
        public string EventType;
        public int FrameId;
        public double UnixTime;
        public double UnityTime;
        public string ObjectLabel;
        public int ExpectedClassId;
        public string ExpectedClassName;
        public bool YoloAvailable;
        public int DetectedClassId;
        public string DetectedClassName;
        public float Confidence;
        public bool ClassMatch;
        public string RoiSource;
        public string AnchorLabel;
        public int RawPoints;
        public int FilteredPoints;
        public float VoxelSizeMeters;
        public string PoseMode;
        public string Shape;
        public string Stability;
        public Vector3 CenterCamera;
        public Vector3 CenterWorld;
        public Vector3 RightCamera;
        public Vector3 UpCamera;
        public Vector3 ForwardCamera;
        public Vector3 ExtentMeters;
        public float GeometryConfidence;
        public float OrientationConfidence;
        public float TrackingConfidence;
        public float OverallConfidence;
        public float DetectMs;
        public float CloudMs;
        public float PoseMs;
        public float ExportMs;
        public float E2eMs;
        public bool ReplacementEnabled;
        public string ModelName;
        public string PlyPath;
        public bool Success;
        public string FailReason;
    }

    public sealed class TrialLogger
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private StreamWriter writer;
        private string csvPath;
        private string summaryPath;
        private int successCount;
        private int classMatchCount;

        public bool IsRecording { get; private set; }
        public string TrialId { get; private set; } = string.Empty;
        public string CsvPath => csvPath;
        public int FrameCount { get; private set; }
        public TrialObjectKind ObjectKind { get; private set; }

        public static string TrialsDirectory => Path.Combine(Application.persistentDataPath, "Trials");

        public bool Start(TrialObjectKind objectKind, string trialId)
        {
            Stop();
            if (objectKind == TrialObjectKind.None || string.IsNullOrEmpty(trialId))
            {
                return false;
            }

            Directory.CreateDirectory(TrialsDirectory);
            TrialId = trialId;
            ObjectKind = objectKind;
            csvPath = Path.Combine(TrialsDirectory, $"{trialId}.csv");
            summaryPath = Path.Combine(TrialsDirectory, $"{trialId}.summary.txt");
            writer = new StreamWriter(csvPath, false, new UTF8Encoding(false));
            writer.WriteLine(Header);
            writer.Flush();
            WriteLatestPointer();
            FrameCount = 0;
            successCount = 0;
            classMatchCount = 0;
            IsRecording = true;
            return true;
        }

        public void Append(TrialFrameRecord record)
        {
            if (!IsRecording || writer == null || record == null)
            {
                return;
            }

            FrameCount++;
            if (record.Success)
            {
                successCount++;
            }

            if (record.ClassMatch)
            {
                classMatchCount++;
            }

            writer.WriteLine(FormatRow(record));
            if (FrameCount % 5 == 0)
            {
                writer.Flush();
            }
        }

        public void Flush()
        {
            writer?.Flush();
            WriteLatestPointer();
        }

        public void Stop()
        {
            if (!IsRecording)
            {
                return;
            }

            try
            {
                writer?.Flush();
                writer?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TrialLogger] Failed to close CSV: {exception.Message}");
            }

            writer = null;
            IsRecording = false;
            WriteSummary();
            WriteLatestPointer();
        }

        private void WriteSummary()
        {
            if (string.IsNullOrEmpty(summaryPath))
            {
                return;
            }

            var matchRate = FrameCount > 0 ? 100f * classMatchCount / FrameCount : 0f;
            var successRate = FrameCount > 0 ? 100f * successCount / FrameCount : 0f;
            var content =
                $"trial_id={TrialId}\n" +
                $"object={TrialObjectCatalog.GetEnglishName(ObjectKind)}\n" +
                $"frames={FrameCount}\n" +
                $"class_match_count={classMatchCount}\n" +
                $"class_match_rate_pct={matchRate.ToString("F1", Invariant)}\n" +
                $"success_count={successCount}\n" +
                $"success_rate_pct={successRate.ToString("F1", Invariant)}\n" +
                $"csv_path={csvPath}\n" +
                $"stopped_at={DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            File.WriteAllText(summaryPath, content);
        }

        private void WriteLatestPointer()
        {
            var pointerPath = Path.Combine(Application.persistentDataPath, "latest_trial_path.txt");
            var content =
                $"trial_id={TrialId}\n" +
                $"recording={(IsRecording ? "yes" : "no")}\n" +
                $"csv_path={csvPath}\n" +
                $"summary_path={summaryPath}\n" +
                $"frames={FrameCount}\n";
            File.WriteAllText(pointerPath, content);
        }

        private const string Header =
            "trial_id,frame_id,event_type,unix_time,unity_time," +
            "object_label,expected_class_id,expected_class_name," +
            "yolo_available,detected_class_id,detected_class_name,confidence,class_match," +
            "roi_source,anchor_label,n_raw,n_filtered,voxel_size_m," +
            "pose_mode,shape,stability," +
            "center_cam_x,center_cam_y,center_cam_z," +
            "center_world_x,center_world_y,center_world_z," +
            "right_x,right_y,right_z,up_x,up_y,up_z,fwd_x,fwd_y,fwd_z," +
            "extent_x,extent_y,extent_z," +
            "geo_conf,orient_conf,track_conf,overall_conf," +
            "t_detect_ms,t_cloud_ms,t_pose_ms,t_export_ms,t_e2e_ms," +
            "replacement_enabled,model_name,ply_path," +
            "success,fail_reason";

        private string FormatRow(TrialFrameRecord record)
        {
            return string.Join(",",
                Escape(TrialId),
                record.FrameId.ToString(Invariant),
                Escape(record.EventType),
                record.UnixTime.ToString("F3", Invariant),
                record.UnityTime.ToString("F3", Invariant),
                Escape(record.ObjectLabel),
                record.ExpectedClassId.ToString(Invariant),
                Escape(record.ExpectedClassName),
                record.YoloAvailable ? "1" : "0",
                record.DetectedClassId.ToString(Invariant),
                Escape(record.DetectedClassName),
                record.Confidence.ToString("F3", Invariant),
                record.ClassMatch ? "1" : "0",
                Escape(record.RoiSource),
                Escape(record.AnchorLabel),
                record.RawPoints.ToString(Invariant),
                record.FilteredPoints.ToString(Invariant),
                record.VoxelSizeMeters.ToString("F4", Invariant),
                Escape(record.PoseMode),
                Escape(record.Shape),
                Escape(record.Stability),
                Format(record.CenterCamera.x),
                Format(record.CenterCamera.y),
                Format(record.CenterCamera.z),
                Format(record.CenterWorld.x),
                Format(record.CenterWorld.y),
                Format(record.CenterWorld.z),
                Format(record.RightCamera.x),
                Format(record.RightCamera.y),
                Format(record.RightCamera.z),
                Format(record.UpCamera.x),
                Format(record.UpCamera.y),
                Format(record.UpCamera.z),
                Format(record.ForwardCamera.x),
                Format(record.ForwardCamera.y),
                Format(record.ForwardCamera.z),
                Format(record.ExtentMeters.x),
                Format(record.ExtentMeters.y),
                Format(record.ExtentMeters.z),
                record.GeometryConfidence.ToString("F3", Invariant),
                record.OrientationConfidence.ToString("F3", Invariant),
                record.TrackingConfidence.ToString("F3", Invariant),
                record.OverallConfidence.ToString("F3", Invariant),
                record.DetectMs.ToString("F2", Invariant),
                record.CloudMs.ToString("F2", Invariant),
                record.PoseMs.ToString("F2", Invariant),
                record.ExportMs.ToString("F2", Invariant),
                record.E2eMs.ToString("F2", Invariant),
                record.ReplacementEnabled ? "1" : "0",
                Escape(record.ModelName),
                Escape(record.PlyPath),
                record.Success ? "1" : "0",
                Escape(record.FailReason));
        }

        private static string Format(float value)
        {
            return value.ToString("F4", Invariant);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            if (value.IndexOfAny(new[] { ',', '"', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
