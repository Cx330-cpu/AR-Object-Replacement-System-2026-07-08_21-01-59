using System.Globalization;
using System.IO;
using System.Text;

namespace ARObjectReplacement.PointCloud
{
    public sealed class PointCloudExporter
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public void ExportPLY(PointCloudData pointCloud, string path)
        {
            var builder = new StringBuilder();
            var count = pointCloud != null && pointCloud.Points != null ? pointCloud.Points.Count : 0;

            builder.AppendLine("ply");
            builder.AppendLine("format ascii 1.0");
            builder.AppendLine("comment AR Object Replacement System M4 camera-coordinate point cloud");
            builder.AppendLine($"element vertex {count}");
            builder.AppendLine("property float x");
            builder.AppendLine("property float y");
            builder.AppendLine("property float z");
            builder.AppendLine("property float confidence");
            builder.AppendLine("end_header");

            if (pointCloud != null && pointCloud.Points != null)
            {
                foreach (var point in pointCloud.Points)
                {
                    builder.Append(point.Position.x.ToString("F6", InvariantCulture)).Append(' ');
                    builder.Append(point.Position.y.ToString("F6", InvariantCulture)).Append(' ');
                    builder.Append(point.Position.z.ToString("F6", InvariantCulture)).Append(' ');
                    builder.Append(point.Confidence.ToString("F4", InvariantCulture)).AppendLine();
                }
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, builder.ToString());
        }

        public void ExportXYZ(PointCloudData pointCloud, string path)
        {
            var builder = new StringBuilder();
            if (pointCloud != null && pointCloud.Points != null)
            {
                foreach (var point in pointCloud.Points)
                {
                    builder.Append(point.Position.x.ToString("F6", InvariantCulture)).Append(' ');
                    builder.Append(point.Position.y.ToString("F6", InvariantCulture)).Append(' ');
                    builder.Append(point.Position.z.ToString("F6", InvariantCulture)).AppendLine();
                }
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, builder.ToString());
        }

        public void ExportPCD(PointCloudData pointCloud, string path)
        {
            throw new System.NotImplementedException("PCD export is reserved for a later experiment milestone.");
        }
    }
}
