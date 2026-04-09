using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife.ChatAI.Entity;
using XCode.DataAccessLayer;

namespace XUnitTest.Gateway;

/// <summary>在当前进程内启动 ChatAI，供集成测试使用</summary>
public class ChatAIWebAppFactory : WebApplicationFactory<Program>
{
    public ChatAIWebAppFactory()
    {
        // 在工厂构造时覆盖连接字符串环境变量，确保在 WebApplication.CreateBuilder 之前生效
        // ASP.NET Core 环境变量规则：ConnectionStrings__ChatAI → ConnectionStrings:ChatAI
        //var dataDir = FindDataDirectory();
        //if (dataDir != null)
        //{
        //    Environment.SetEnvironmentVariable("ConnectionStrings__ChatAI",
        //        $"Data Source={Path.Combine(dataDir, "ChatAI.db")};Provider=SQLite");
        //    Environment.SetEnvironmentVariable("ConnectionStrings__Membership",
        //        $"Data Source={Path.Combine(dataDir, "Membership.db")};Provider=SQLite");
        //}

        DAL.AddConnStr("ChatAI", "Data Source=..\\Data\\ChatAI.db;Provider=SQLite", null, "SQLite");
        DAL.AddConnStr("Membership", "Data Source=..\\Data\\Membership.db;Provider=SQLite", null, "SQLite");
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var projectRoot = FindProjectRoot("NewLife.ChatAI");
        if (projectRoot != null)
            builder.UseContentRoot(projectRoot);
    }

    /// <inheritdoc/>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        SeedTestData();
        return host;
    }

    /// <inheritdoc/>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // 宿主销毁后 ObjectContainer.Current 仍持有已销毁的 IServiceProvider 引用。
            // 替换为空容器，避免并行运行的其它测试通过 NewLife.Serialization.SystemJson
            // 调用 ServiceTypeResolver.Modifier 时抛出 ObjectDisposedException。
            try
            {
                if (NewLife.Model.ObjectContainer.Current is NewLife.Model.ObjectContainer)
                    NewLife.Model.ObjectContainer.SetInnerProvider(new ServiceCollection().BuildServiceProvider());
            }
            catch { }
        }
    }

    /// <summary>确保集成测试所需的提供商和模型已启用。对全新数据库（InitData 尚未运行）和已有数据库均有效</summary>
    private static void SeedTestData()
    {
        try
        {
            // 1. 触发 ProviderConfig.InitData()（若表为空）并确保 DashScope 已启用
            var providers = ProviderConfig.FindAll();
            var dashScope = providers.FirstOrDefault(p => p.Code == "DashScope");
            if (dashScope == null) return;

            if (!dashScope.Enable)
            {
                dashScope.Enable = true;
                dashScope.Save();
            }

            // 2. 触发 ModelConfig.InitData()（若表为空）并确保 qwen3.5-flash 已启用
            //    全新数据库：InitData 在此时读取到已启用的 DashScope，会以 Enable=true 创建模型
            //    已有数据库：直接更新现有记录
            var models = ModelConfig.FindAll();
            var model = models.FirstOrDefault(m => m.Code == "qwen3.5-flash");
            if (model == null)
            {
                model = new ModelConfig
                {
                    Code = "qwen3.5-flash",
                    Name = "Qwen3.5 Flash",
                    ProviderId = dashScope.Id,
                    Enable = true,
                    Sort = 1,
                };
                model.Insert();
            }
            else if (!model.Enable)
            {
                model.Enable = true;
                model.Save();
            }
        }
        catch
        {
            // 种子数据失败不应中断测试启动，单个测试会以明确断言提示数据缺失
        }
    }

    ///// <summary>从测试输出目录向上查找包含 Data/ChatAI.db 的目录</summary>
    //private static String? FindDataDirectory()
    //{
    //    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    //    while (dir != null)
    //    {
    //        var dataPath = Path.Combine(dir.FullName, "Data");
    //        if (File.Exists(Path.Combine(dataPath, "ChatAI.db")))
    //            return dataPath;
    //        dir = dir.Parent;
    //    }
    //    return null;
    //}

    /// <summary>从测试输出目录向上查找指定子目录</summary>
    private static String? FindProjectRoot(String projectDirName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, projectDirName);
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
