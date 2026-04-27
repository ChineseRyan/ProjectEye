using ProjectEye.Core.Models.Statistic;
using System;
using System.Data.Entity;
using System.Data.SQLite;
using System.IO;

namespace ProjectEye.Database
{
    public class StatisticContext : DbContext
    {
        /// <summary>
        /// 统计数据
        /// </summary>
        public DbSet<StatisticModel> Statistics { get; set; }
        /// <summary>
        /// 番茄数据
        /// </summary>
        public DbSet<Core.Models.Statistic.TomatoModel> Tomatos { get; set; }
        // public StatisticContext(string n)
        //: base("StatisticContext")
        // {
        //     DbConfiguration.SetConfiguration(new SQLiteConfiguration());
        // }
        public StatisticContext()
       : base(new SQLiteConnection()
       {
           ConnectionString = "Data Source=.\\Data\\data.db"
       }, true)
        {
            // SQLite 不支持 EF 的 CreateDatabaseIfNotExists 策略，
            // 必须禁用自动建库，改用 SQLiteBuilder 手动管理表结构
            // 使用完全限定名调用静态方法，避免与 DbContext.Database 实例属性混淆
            System.Data.Entity.Database.SetInitializer<StatisticContext>(null);
            DbConfiguration.SetConfiguration(new SQLiteConfiguration());
            EnsureDatabaseExists();
        }

        /// <summary>
        /// 确保 Data 目录和数据库文件存在。
        /// SQLite 不会自动创建目录，首次运行时必须手动创建空数据库文件。
        /// </summary>
        private void EnsureDatabaseExists()
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.db");
            if (!File.Exists(dbPath))
            {
                var dir = Path.GetDirectoryName(dbPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                // 创建空的 SQLite 数据库文件
                SQLiteConnection.CreateFile(dbPath);
            }
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            var model = modelBuilder.Build(Database.Connection);
            new SQLiteBuilder(model).Handle();
        }
    }
}