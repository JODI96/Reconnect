namespace Reconnect.Contracts
{
    /// <summary>HTTP routes shared by backend and Unity client.</summary>
    public static class ApiRoutes
    {
        public static class Auth
        {
            public const string Group = "/auth";
            public const string Register = Group + "/register";
            public const string Login = Group + "/login";
            public const string Refresh = Group + "/refresh";
        }

        public static class Profiles
        {
            public const string Group = "/profiles";
            public const string Me = Group + "/me";
            public static string ById(System.Guid id) => Group + "/" + id;
        }

        public static class Rooms
        {
            public const string Group = "/rooms";
            public static string ById(System.Guid id) => Group + "/" + id;
            public static string Layout(System.Guid id) => Group + "/" + id + "/layout";
        }

        public static class Buildings
        {
            public const string Group = "/buildings";
            public const string Nearby = Group + "/nearby";
        }

        public static class Likes
        {
            public const string Group = "/likes";
            public static string ForUser(System.Guid userId) => Group + "/" + userId;
        }

        public static class Blocks
        {
            public const string Group = "/blocks";
            public static string ForUser(System.Guid userId) => Group + "/" + userId;
        }

        public static class Reports
        {
            public const string Group = "/reports";
        }
    }
}
