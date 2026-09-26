namespace Reconnect.Contracts
{
    /// <summary>
    /// HTTP routes shared by backend and Unity client. All endpoints live under a version prefix, so
    /// app versions already in the stores keep working when a new API version is introduced.
    /// <c>Path</c> = relative to the version group (used by the backend), <c>Group</c>/others = full paths (clients).
    /// </summary>
    public static class ApiRoutes
    {
        public const string Version = "/v1";

        public static class Auth
        {
            public const string Path = "/auth";
            public const string Group = Version + Path;
            public const string Register = Group + "/register";
            public const string Login = Group + "/login";
            public const string Refresh = Group + "/refresh";
        }

        public static class Profiles
        {
            public const string Path = "/profiles";
            public const string Group = Version + Path;
            public const string Me = Group + "/me";
            public static string ById(System.Guid id) => Group + "/" + id;
        }

        public static class Rooms
        {
            public const string Path = "/rooms";
            public const string Group = Version + Path;
            public static string ById(System.Guid id) => Group + "/" + id;
            public static string Layout(System.Guid id) => Group + "/" + id + "/layout";
        }

        public static class Buildings
        {
            public const string Path = "/buildings";
            public const string Group = Version + Path;
            public const string Nearby = Group + "/nearby";
        }

        public static class Likes
        {
            public const string Path = "/likes";
            public const string Group = Version + Path;
            public static string ForUser(System.Guid userId) => Group + "/" + userId;
        }

        public static class Blocks
        {
            public const string Path = "/blocks";
            public const string Group = Version + Path;
            public static string ForUser(System.Guid userId) => Group + "/" + userId;
        }

        public static class Reports
        {
            public const string Path = "/reports";
            public const string Group = Version + Path;
        }
    }
}
