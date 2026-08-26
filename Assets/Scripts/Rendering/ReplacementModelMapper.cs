using ARObjectReplacement.Detection;

namespace ARObjectReplacement.Rendering
{
    public static class ReplacementModelMapper
    {
        public static string GetResourceName(int classId)
        {
            switch (classId)
            {
                case 26:
                    return "帆布包";
                case 28:
                    return "手提箱";
                case 39:
                    return "酒";
                case 40:
                    return "酒";
                case 41:
                    return "酒";
                case 62:
                    return "tv__old_tv__retro_tv(1)";
                case 63:
                    return "电脑";
                case 67:
                    return "手持电话";
                case 74:
                    return "怀表";
                case 75:
                    return "玻璃罐";
                default:
                    return FallbackByClassName(CocoClassNames.GetName(classId));
            }
        }

        private static string FallbackByClassName(string className)
        {
            switch (className)
            {
                case "suitcase":
                    return "手提箱";
                case "handbag":
                    return "帆布包";
                case "bottle":
                    return "酒";
                case "wine glass":
                    return "酒";
                case "cup":
                    return "酒";
                case "tv":
                    return "tv__old_tv__retro_tv(1)";
                case "laptop":
                    return "电脑";
                case "cell phone":
                    return "手持电话";
                case "clock":
                    return "怀表";
                case "vase":
                    return "玻璃罐";
                default:
                    return "DefaultReplacement";
            }
        }
    }
}
