namespace ARObjectReplacement.Evaluation
{
    public enum TrialObjectKind
    {
        None = 0,
        Cup = 1,
        Phone = 2,
        Laptop = 3
    }

    public static class TrialObjectCatalog
    {
        public static string GetChineseLabel(TrialObjectKind kind)
        {
            switch (kind)
            {
                case TrialObjectKind.Cup:
                    return "杯子";
                case TrialObjectKind.Phone:
                    return "手机";
                case TrialObjectKind.Laptop:
                    return "电脑";
                default:
                    return "未选物体";
            }
        }

        public static string GetEnglishName(TrialObjectKind kind)
        {
            switch (kind)
            {
                case TrialObjectKind.Cup:
                    return "cup";
                case TrialObjectKind.Phone:
                    return "phone";
                case TrialObjectKind.Laptop:
                    return "laptop";
                default:
                    return "none";
            }
        }

        public static int GetForcedClassId(TrialObjectKind kind)
        {
            switch (kind)
            {
                case TrialObjectKind.Cup:
                    return 41;
                case TrialObjectKind.Phone:
                    return 67;
                case TrialObjectKind.Laptop:
                    return 63;
                default:
                    return -1;
            }
        }

        public static string GetExpectedClassName(TrialObjectKind kind)
        {
            switch (kind)
            {
                case TrialObjectKind.Cup:
                    return "cup";
                case TrialObjectKind.Phone:
                    return "cell phone";
                case TrialObjectKind.Laptop:
                    return "laptop";
                default:
                    return "none";
            }
        }

        public static string GetReplacementResourceName(TrialObjectKind kind)
        {
            switch (kind)
            {
                case TrialObjectKind.Cup:
                    return "酒";
                case TrialObjectKind.Phone:
                    return "手持电话";
                case TrialObjectKind.Laptop:
                    return "电脑";
                default:
                    return "DefaultReplacement";
            }
        }

        public static bool MatchesDetectedClass(TrialObjectKind kind, int classId)
        {
            switch (kind)
            {
                case TrialObjectKind.Cup:
                    return classId == 41 || classId == 40 || classId == 39;
                case TrialObjectKind.Phone:
                    return classId == 67 || classId == 65;
                case TrialObjectKind.Laptop:
                    return classId == 63;
                default:
                    return false;
            }
        }
    }
}
