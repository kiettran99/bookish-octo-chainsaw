namespace Common.ValueObjects;

public static class CommonConst
{
    public static class RoleName
    {
        public const string User = "User";
        public const string VipUser = "Vip User";
        public const string Partner = "Partner";
        public const string Admin = "Admin";
    }

    public static class CacheKeys
    {
        public static class CMS
        {
            public const string CountryDropdown = "CountryDropdown";
            public const string ProvinceDropdown = "ProvinceDropdown";
            public const string TravelDestinationTypeDropdown = "TravelDestinationTypeDropdown";
            public const string TravelDestinationDropDown = "TravelDestinationDropdown";
        }

        public static class Portal
        {
            public const string ProvinceDropdown = "ProvinceDropdown_{0}";
            public const string ProvinceDetail = "ProvinceDetail_{0}";
            public const string ProvinceRelated = "ProvinceRelated_{0}_{1}";
            public const string TravelDestinationTypeDropdown = "TravelDestinationTypeDropdown_{0}";
            public const string TravelDestinationTypeDetail = "TravelDestinationTypeDetail_{0}";
            public const string CountryDropdown = "CountryDropdown_{0}";
            public const string TravelDestinationAll = "TravelDestinationAll_{0}";
            public const string TravelDestinationRelated = "TravelDestinationRelated_{0}_{1}";
            public const string TravelPostRelated = "TravelPostRelated_{0}_{1}";
            public const string HomePage = "HomePage_{0}";

            public const string UserCheckInProfile = "UserCheckInProfile_PageNumber_{0}_PageSize_{1}_UserId_{2}";
            public const string UserCheckInProfileRowNum = "UserCheckInProfileRowNum_PageNumber_{0}_PageSize_{1}_UserId_{2}";
            public const string UserAchievements = "UserAchievements_UserId_{0}";
        }
    }
}