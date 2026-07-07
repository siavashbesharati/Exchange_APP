namespace ForexExchange.Models
{
    public static class Permissions
    {
        // Order Management
        public const string Order_View = "مشاهده معاملات";
        public const string Order_Create = "ثبت معامله";
        public const string Order_Edit = "ویرایش معامله";
        public const string Order_Delete = "حذف معامله";


        // Document Management
        public const string Documents_View = "مشاهده اسناد";
        public const string Documents_Create = "ثبت سند";
        public const string Documents_Edit = "ویرایش سند";
        public const string Documents_Delete = "حذف سند";
        public const string Documents_Confirm = "تایید سند";

        // Reports
        public const string Reports = " گزارشات";
        public const string Customer_Reports = "گزارش مشتریان";
        public const string All_Customers_Balances = "موجودی همه مشتریان";
        public const string Bank_Account_Reports = "گزارشات حساب‌های بانکی ";
        public const string Pool_Reports = "گزارش داشبورد";
        public const string Order_Reports = "گزارش معاملات";
        public const string Document_Reports = "گزارش اسناد";
        public const string Pool_Summary_Reports = "خلاصه سود/زیان روزانه";
        public const string Customer_BankHistory_Report = " تراز کلی ";
        public const string Expenses_Report = "گزارش هزینه‌ها";


        //Tasks
        public const string Tasks_View = "مشاهده تسک‌ها";
        public const string Tasks_Detail = "مشاهده جزئیات تسک";
        public const string Tasks_Create = "ثبت تسک";
        public const string Tasks_Edit = "ویرایش تسک";
        public const string Tasks_Delete = "حذف تسک";


        //Advance management
        public const string Advance_Management = "مدیریت پیشرفته";


        //bankAccounts
        public const string Bank_Accounts_View = "مشاهده حساب‌های بانکی";
        public const string Bank_Accounts_Create = "افزودن حساب‌های بانکی";
        public const string Bank_Accounts_Edit = "ویرایش حساب‌های بانکی";
        public const string Bank_Accounts_Delete = "حذف حساب‌های بانکی";
        
        





        // User Management
        public const string Users_View = "Permissions.Users.View";
        public const string Users_Create = "Permissions.Users.Create";
        public const string Users_Edit = "Permissions.Users.Edit";
        public const string Users_ChangeRole = "Permissions.Users.ChangeRole";
        public const string Users_Delete = "Permissions.Users.Delete";
        public const string Users_RegenerateTotpSecret = "Permissions.Users.RegenerateTotpSecret";
        public const string Users_ResetAllSessions = "Permissions.Users.ResetAllSessions";

        // Add more permissions as needed
    }
}
