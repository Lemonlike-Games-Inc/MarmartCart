namespace LightSide
{
    /// <summary>
    /// Single source of truth for the editor, component, asset-creation and Project Settings menu paths
    /// contributed by the Core package.
    /// </summary>
    public static class LightSideMenu
    {
        private const string Root = "LightSide";

        /// <summary>Project Settings root; a package nests its own page as <c>ProjectSettingsRoot + "/" + name</c>.</summary>
        public const string ProjectSettingsRoot = "Project/" + Root;

        internal static class Palette
        {
            public const string ShortcutId = Root + "/Commands";
        }

        internal static class Tools
        {
            private const string P = "Tools/" + Root + "/";
            public const string LogZones = P + "Log Zones";
            public const string NoiseGenerator = P + "Noise Generator";
            public const string PackagePatcher = P + "Package Patcher";
        }

        internal static class AddComponent
        {
            private const string P = Root + "/";
            public const string EventSystemBootstrap = P + "Event System Bootstrap";
            public const string FocusGuard = P + "Focus Guard";
            public const string DraggableRect = P + "Draggable Rect";
            public const string MoveItPlayer = P + "MoveIt Player";
        }

        internal static class CreateAsset
        {
            private const string P = Root + "/";
            public const string MoveIt = P + "MoveIt";
        }

        internal static class Window
        {
            private const string P = "Window/" + Root + "/";
            public const string Timeline = P + "Timeline";
            public const string MoveItDebugger = P + "MoveIt Debugger";
        }
    }
}
