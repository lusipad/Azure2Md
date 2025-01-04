using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Newtonsoft.Json;
using Microsoft.VisualStudio.Services.OAuth;

public class AppSettings
{
    public string TfsUrl { get; set; }
    public List<ProjectConfig> Projects { get; set; }
    public string PersonalAccessToken { get; set; }
    public ReportSettings ReportSettings { get; set; }
}

public class ProjectConfig
{
    public string ProjectName { get; set; }
    public QuerySettings Query { get; set; }
    public WorkItemFieldMappings FieldMappings { get; set; } = new WorkItemFieldMappings();
}

public class QuerySettings
{
    public bool UseExistingQuery { get; set; }
    public string QueryPath { get; set; }
    public string CustomWiql { get; set; }
}

public class ReportSettings
{
    public bool MergeProjects { get; set; }
    public string MergedTitle { get; set; }
    public string Language { get; set; } = "auto";
    public DisplayOptions DisplayOptions { get; set; } = new DisplayOptions();
}

public class DisplayOptions
{
    public bool ShowFeatureInGantt { get; set; } = true;
    public bool ShowUserStoryInGantt { get; set; } = true;
    public bool PrefixParentName { get; set; } = true;
}

public class WorkItemFieldMappings
{
    public WorkItemTypeFields Feature { get; set; } = new WorkItemTypeFields 
    {
        StartDateField = "Microsoft.VSTS.Scheduling.StartDate",
        EndDateField = "Microsoft.VSTS.Scheduling.FinishDate"
    };
    public WorkItemTypeFields UserStory { get; set; } = new WorkItemTypeFields
    {
        StartDateField = "Microsoft.VSTS.Scheduling.StartDate",
        EndDateField = "Microsoft.VSTS.Scheduling.FinishDate"
    };
    public WorkItemTypeFields Task { get; set; } = new WorkItemTypeFields
    {
        StartDateField = "Microsoft.VSTS.Scheduling.StartDate",
        EndDateField = "Microsoft.VSTS.Scheduling.FinishDate"
    };
}

public class WorkItemTypeFields
{
    public string StartDateField { get; set; }
    public string EndDateField { get; set; }
}

// 语言资源管理类
public static class LanguageResources
{
    // 多语言资源字典
    private static readonly Dictionary<string, Dictionary<string, string>> Resources = new()
    {
        ["zh-CN"] = new()
        {
            ["ReportTitle"] = "工作项报告",
            ["GeneratedTime"] = "生成时间",
            ["Overview"] = "总体概览",
            ["TotalItems"] = "工作项总数",
            ["CompletedItems"] = "完成项数量",
            ["ActiveItems"] = "进行中数量",
            ["ProjectStats"] = "项目统计",
            ["Gantt"] = "甘特图",
            ["FeatureView"] = "Feature 层级视图",
            ["UserStoryView"] = "User Story 层级视图",
            ["WorkItems"] = "工作项分类",
            ["TeamMembers"] = "团队成员任务分配",
            ["PersonalGantt"] = "个人甘特图",
            ["WorkItemList"] = "工作项列表",
            ["Unassigned"] = "未分配",
            ["OtherTasks"] = "其他任务",
            ["ViewInAzure"] = "在 Azure DevOps 中查看项目",
            ["ID"] = "ID",
            ["Title"] = "标题",
            ["Status"] = "状态",
            ["Assignee"] = "负责",
            ["StartDate"] = "开始日期",
            ["EndDate"] = "结束日期",
            ["ParentStory"] = "所属 Story",
            ["Type"] = "类型",
            ["ItemTitle"] = "标题"
        },
        ["en-US"] = new()
        {
            ["ReportTitle"] = "Work Items Report",
            ["GeneratedTime"] = "Generated Time",
            ["Overview"] = "Overview",
            ["TotalItems"] = "Total Items",
            ["CompletedItems"] = "Completed Items",
            ["ActiveItems"] = "Active Items",
            ["ProjectStats"] = "Project Statistics",
            ["Gantt"] = "Gantt Chart",
            ["FeatureView"] = "Feature Level View",
            ["UserStoryView"] = "User Story Level View",
            ["WorkItems"] = "Work Items",
            ["TeamMembers"] = "Team Member Assignments",
            ["PersonalGantt"] = "Personal Gantt",
            ["WorkItemList"] = "Work Item List",
            ["Unassigned"] = "Unassigned",
            ["OtherTasks"] = "Other Tasks",
            ["ViewInAzure"] = "View in Azure DevOps",
            ["ID"] = "ID",
            ["Title"] = "Title",
            ["Status"] = "Status",
            ["Assignee"] = "Assignee",
            ["StartDate"] = "Start Date",
            ["EndDate"] = "End Date",
            ["ParentStory"] = "Parent Story",
            ["Type"] = "Type",
            ["ItemTitle"] = "Title"
        }
    };

    public static string GetText(string language, string key)
    {
        if (!Resources.ContainsKey(language))
            language = "en-US"; // 默认英文

        return Resources[language].GetValueOrDefault(key, key);
    }
}

// 语言辅助类
public static class LanguageHelper
{
    // 获取当前使用的语言
    public static string GetCurrentLanguage(string configLanguage)
    {
        // 如果配置不是 auto，直接使用配置的语言
        if (!string.Equals(configLanguage, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return configLanguage;
        }

        try
        {
            // 获取系统当前 UI 文化
            var currentUICulture = System.Threading.Thread.CurrentThread.CurrentUICulture;
            
            // 根据系统语言选择支持的语言
            return currentUICulture.Name.ToLower() switch
            {
                var x when x.StartsWith("zh") => "zh-CN",
                var x when x.StartsWith("en") => "en-US",
                _ => "en-US"  // 默认使用英语
            };
        }
        catch
        {
            return "en-US";  // 出错时默认使用英语
        }
    }
}

public class Program
{
    // 全局配置对象
    private static AppSettings settings;

    public static void Main()
    {
        try
        {
            // 读取和验证配置
            string configPath = "appsettings.json";
            if (!File.Exists(configPath))
            {
                Console.WriteLine("错误：找不到配置文件 appsettings.json");
                Console.WriteLine("\n请创建配置文件 appsettings.json，内容示例：");
                Console.WriteLine(GetConfigExample());
                return;
            }

            var config = File.ReadAllText(configPath, Encoding.UTF8);
            settings = JsonConvert.DeserializeObject<AppSettings>(config);

            // 验证必要的配置项
            var validationError = ValidateSettings(settings);
            if (!string.IsNullOrEmpty(validationError))
            {
                Console.WriteLine($"错误：配置文件验证失败 - {validationError}");
                Console.WriteLine("\n正确的配置文件示例：");
                Console.WriteLine(GetConfigExample());
                return;
            }

            // 配置 HTTP 和 TLS 设置
            ConfigureHttpAndTlsSettings();

            // 创建 HTTP 客户端设置
            var clientSettings = new VssClientHttpRequestSettings
            {
                MaxRetryRequest = 5,
                SendTimeout = TimeSpan.FromMinutes(5)
            };

            // 创建连接凭据
            var credentials = new VssOAuthAccessTokenCredential(settings.PersonalAccessToken);

            // 创建 Azure DevOps/TFS 连接
            VssConnection connection = new VssConnection(new Uri(settings.TfsUrl), credentials, clientSettings);
            
            // 测试连接
            Console.WriteLine("正在测试连接...");
            try
            {
                connection.ConnectAsync().Wait();
                Console.WriteLine("连接成功！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"连接失败：{ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"详细错误：{ex.InnerException.Message}");
                }
                return;
            }

            var witClient = connection.GetClient<WorkItemTrackingHttpClient>();

            if (settings.ReportSettings?.MergeProjects == true)
            {
                // 创建总报告的 StringBuilder
                var totalReport = new StringBuilder();
                totalReport.AppendLine("# " + (settings.ReportSettings.MergedTitle ?? "Azure DevOps 工作项报告"));
                totalReport.AppendLine();
                totalReport.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                totalReport.AppendLine();

                // 收集所有项目的工作项
                var allWorkItems = new List<(WorkItem Item, string ProjectName)>();
                
                foreach (var project in settings.Projects)
                {
                    Console.WriteLine($"正在处理项目：{project.ProjectName}");
                    var projectItems = GetProjectWorkItems(witClient, project);
                    if (projectItems != null)
                    {
                        allWorkItems.AddRange(projectItems.Select(item => (item, project.ProjectName)));
                    }
                }

                if (allWorkItems.Any())
                {
                    GenerateMergedReport(allWorkItems, settings.TfsUrl, totalReport);
                }

                // 保存总报告
                string outputFile = "work_items_report.md";
                File.WriteAllText(outputFile, totalReport.ToString(), Encoding.UTF8);
                Console.WriteLine($"总报告已生成：{outputFile}");
            }
            else
            {
                // 分别处理每个项目
                foreach (var project in settings.Projects)
                {
                    Console.WriteLine($"正在处理项目：{project.ProjectName}");
                    
                    // 创建项目报告的 StringBuilder
                    var projectReport = new StringBuilder();
                    projectReport.AppendLine($"# {project.ProjectName} 工作项报告");
                    projectReport.AppendLine();
                    projectReport.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    projectReport.AppendLine();

                    // 处理项目
                    ProcessProject(witClient, project, settings.TfsUrl, projectReport);

                    // 保存项目报告
                    string outputFile = $"work_items_{project.ProjectName}.md";
                    File.WriteAllText(outputFile, projectReport.ToString(), Encoding.UTF8);
                    Console.WriteLine($"项目 {project.ProjectName} 的报告已生成：{outputFile}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理错误：{ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"详细错误：{ex.InnerException.Message}");
            }
        }
    }

    private static void ConfigureHttpAndTlsSettings()
    {
        try
        {
            // 配置 TLS 设置
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            // 配置证书验证
            if (settings.TfsUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("检测到 HTTP 连接，配置安全设置...");
                
                // 禁用证书验证
                ServicePointManager.ServerCertificateValidationCallback = delegate (
                    object sender,
                    X509Certificate certificate,
                    X509Chain chain,
                    SslPolicyErrors sslPolicyErrors)
                {
                    return true; // 允许所有证书
                };
            }

            // 配置 HTTP 设置
            ServicePointManager.DefaultConnectionLimit = 100;
            ServicePointManager.MaxServicePointIdleTime = 10000;
            ServicePointManager.CheckCertificateRevocationList = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"配置 HTTP/TLS 设置时发生错误：{ex.Message}");
            throw;
        }
    }

    private static void ProcessProject(WorkItemTrackingHttpClient witClient, ProjectConfig project, string tfsUrl, StringBuilder totalReport)
    {
        try
        {
            // 创建或使用现有查询
            Wiql wiql;
            if (project.Query?.UseExistingQuery == true && !string.IsNullOrEmpty(project.Query.QueryPath))
            {
                try
                {
                    var query = witClient.GetQueryAsync(project.ProjectName, project.Query.QueryPath).Result;
                    wiql = query != null ? new Wiql { Query = query.Wiql } : CreateDefaultWiql(project.ProjectName);
                }
                catch
                {
                    wiql = CreateDefaultWiql(project.ProjectName);
                }
            }
            else
            {
                wiql = CreateDefaultWiql(project.ProjectName);
            }

            // 执行查询
            var result = witClient.QueryByWiqlAsync(wiql).Result;
            var workItemIds = result.WorkItems.Select(item => item.Id).ToArray();

            if (workItemIds.Length > 0)
            {
                var workItems = witClient.GetWorkItemsAsync(workItemIds, expand: WorkItemExpand.All).Result;
                
                // 将项目报告添加到总报告中
                GenerateReport(workItems, project.ProjectName, tfsUrl, totalReport);
                
                Console.WriteLine($"项目 {project.ProjectName} 的报告已生成");
            }
            else
            {
                Console.WriteLine($"项目 {project.ProjectName} 未找到任何工作项。");
                totalReport.AppendLine($"## {project.ProjectName}");
                totalReport.AppendLine();
                totalReport.AppendLine("未找到任何工作项。");
                totalReport.AppendLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理项目 {project.ProjectName} 时发生错误：{ex.Message}");
            totalReport.AppendLine($"## {project.ProjectName}");
            totalReport.AppendLine();
            totalReport.AppendLine($"处理出错：{ex.Message}");
            totalReport.AppendLine();
        }
    }

    private static void GenerateReport(IEnumerable<WorkItem> workItems, string projectName, string tfsUrl, StringBuilder sb)
    {
        // 获取当前应使用的语言
        var currentLanguage = LanguageHelper.GetCurrentLanguage(settings.ReportSettings?.Language ?? "auto");
        var t = (string key) => LanguageResources.GetText(currentLanguage, key);

        // 项目标题使用二级标题
        sb.AppendLine($"## {projectName}");
        sb.AppendLine();
        
        // 添加项目链接
        sb.AppendLine($"[{t("ViewInAzure")}]({tfsUrl}/{projectName})");
        sb.AppendLine();

        // 1. 项目概览
        sb.AppendLine($"### {t("Overview")}");
        sb.AppendLine();
        sb.AppendLine($"- {t("TotalItems")}：{workItems.Count()}");
        sb.AppendLine($"- {t("CompletedItems")}：{workItems.Count(w => GetTaskStatus(w.Fields["System.State"].ToString()) == "done")}");
        sb.AppendLine($"- {t("ActiveItems")}：{workItems.Count(w => GetTaskStatus(w.Fields["System.State"].ToString()) == "active")}");
        sb.AppendLine();

        // 2. 甘特图部分（使用三级标题）
        sb.AppendLine("### 项目甘特图");

        // 2.1 Feature + User Story 视图
        sb.AppendLine("#### Feature 层级视图");
        GenerateFeatureGanttChart(sb, $"{projectName} Feature 进度", workItems);

        // 2.2 User Story + Task 视图
        sb.AppendLine("#### User Story 层级视图");
        GenerateUserStoryGanttChart(sb, $"{projectName} User Story 进度", workItems);

        // 3. 按工作项类型组（使用三级标题）
        sb.AppendLine("### 工作项分类");
        
        // 3.1 User Stories（使用四级标题）
        var userStories = workItems.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "User Story").ToList();
        if (userStories.Any())
        {
            sb.AppendLine("#### User Stories");
            sb.AppendLine();
            sb.AppendLine("| ID | 标题 | 状态 | 负责人 | 开始日期 | 结束日期 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var story in userStories)
            {
                var id = story.Id;
                var title = story.Fields["System.Title"].ToString();
                var state = story.Fields["System.State"].ToString();
                var assignedTo = GetPersonName(
                    story.Fields.ContainsKey("System.AssignedTo") 
                        ? story.Fields["System.AssignedTo"] 
                        : null);
                var startDate = GetStartDate(story, settings.Projects.First(p => p.ProjectName == projectName));
                var endDate = GetEndDate(story, settings.Projects.First(p => p.ProjectName == projectName));

                sb.AppendLine($"| {id} | {title} | {state} | {assignedTo} | {startDate} | {endDate} |");
            }
            sb.AppendLine();
        }

        // 3.2 Tasks（使用四级标题）
        var tasks = workItems.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "Task").ToList();
        if (tasks.Any())
        {
            sb.AppendLine("#### Tasks");
            sb.AppendLine();
            sb.AppendLine("| ID | 标题 | 状态 | 负责人 | 开始日期 | 结束日期 | 所属 Story |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var task in tasks)
            {
                var id = task.Id;
                var title = task.Fields["System.Title"].ToString();
                var state = task.Fields["System.State"].ToString();
                var assignedTo = GetPersonName(
                    task.Fields.ContainsKey("System.AssignedTo") 
                        ? task.Fields["System.AssignedTo"] 
                        : null);
                var startDate = GetStartDate(task, settings.Projects.First(p => p.ProjectName == projectName));
                var endDate = GetEndDate(task, settings.Projects.First(p => p.ProjectName == projectName));
                var parentId = task.Fields.ContainsKey("System.Parent") 
                    ? task.Fields["System.Parent"].ToString() 
                    : "-";

                sb.AppendLine($"| {id} | {title} | {state} | {assignedTo} | {startDate} | {endDate} | {parentId} |");
            }
            sb.AppendLine();
        }

        // 4. 按人员分组的视图（使用三级标题）
        sb.AppendLine("### 团队成员任务分配");
        var workItemsByPerson = workItems
            .GroupBy(item => GetPersonName(
                item.Fields.ContainsKey("System.AssignedTo") 
                    ? item.Fields["System.AssignedTo"] 
                    : null));

        foreach (var personGroup in workItemsByPerson)
        {
            // 每个人的任务（使用四级标题）
            sb.AppendLine($"#### {personGroup.Key}");
            sb.AppendLine();
            
            // 个人甘特图（使用五级标题）
            sb.AppendLine("##### 个人甘特图");
            // 根据工作项类型选择合适的甘特图生成方法
            var hasFeatures = personGroup.Any(i => i.Fields["System.WorkItemType"].ToString() == "Feature");
            if (hasFeatures)
            {
                // 如果有 Feature，使用 Feature 层级视图
                GenerateFeatureGanttChart(sb, $"{personGroup.Key}的进度甘特图", personGroup);
            }
            else
            {
                // 否则使用 User Story 层级视图
                GenerateUserStoryGanttChart(sb, $"{personGroup.Key}的进度特图", personGroup);
            }
            
            // 个人工作项列表（使用五级标题）
            sb.AppendLine("#### 工作项列表");
            sb.AppendLine();
            sb.AppendLine("| ID | 类型 | 标题 | 状态 | 开始日期 | 结束日期 |");
            sb.AppendLine("|---|---|---|---|---|---|");

            foreach (var item in personGroup)
            {
                var id = item.Id;
                var type = item.Fields["System.WorkItemType"].ToString();
                var title = item.Fields["System.Title"].ToString();
                var state = item.Fields["System.State"].ToString();
                var startDate = GetStartDate(item, settings.Projects.First(p => p.ProjectName == projectName));
                var endDate = GetEndDate(item, settings.Projects.First(p => p.ProjectName == projectName));

                sb.AppendLine($"| {id} | {type} | {title} | {state} | {startDate} | {endDate} |");
            }
            
            sb.AppendLine();
        }

        // 添加分隔线
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static Wiql CreateDefaultWiql(string projectName)
    {
        string defaultWiql = 
            "Select [System.Id], [System.Title], [System.State], [System.AssignedTo], " +
            "[Microsoft.VSTS.Scheduling.StartDate], [Microsoft.VSTS.Scheduling.FinishDate], " +
            "[System.WorkItemType], [System.Parent] " +
            $"From WorkItems Where [System.TeamProject] = '{projectName}' " +
            "And ([System.WorkItemType] = 'Feature' Or [System.WorkItemType] = 'User Story' Or [System.WorkItemType] = 'Task') " +
            "Order By [System.Id]";

        return new Wiql { Query = defaultWiql };
    }

    private static string FormatDate(object dateValue)
    {
        if (dateValue == null) return DateTime.Now.ToString("yyyy-MM-dd");

        if (DateTime.TryParse(dateValue.ToString(), out DateTime date))
        {
            // 将 UTC 时间转换为本地时间
            if (date.Kind == DateTimeKind.Utc)
            {
                date = date.ToLocalTime();
            }
            return date.ToString("yyyy-MM-dd");
        }
        
        return DateTime.Now.ToString("yyyy-MM-dd");
    }

    private static string GetPersonName(object assignedTo)
    {
        if (assignedTo == null) return "未分配";

        try
        {
            // Azure DevOps API 返回的是 IdentityRef 对象
            if (assignedTo is Microsoft.VisualStudio.Services.WebApi.IdentityRef identityRef)
            {
                return identityRef.DisplayName ?? "未分配";
            }
            
            // 如果是字符串，尝试解析 JSON
            if (assignedTo is string assignedToString)
            {
                var identity = JsonConvert.DeserializeObject<Dictionary<string, object>>(assignedToString);
                return identity.ContainsKey("displayName") ? identity["displayName"].ToString() : "未分配";
            }
        }
        catch
        {
            // 如果解析失败，直接返回字符串示
            return assignedTo.ToString();
        }

        return "未分配";
    }

    private static string GetTaskStatus(string state)
    {
        // 根据 Azure DevOps 的状态映射到 Mermaid 甘特图的状态
        return state.ToLower() switch
        {
            "done" => "done",
            "closed" => "done",
            "completed" => "done",
            "resolved" => "done",
            "removed" => "done",
            "active" => "active",
            "in progress" => "active",
            "doing" => "active",
            _ => ""  // 其他状态不添加特殊标记
        };
    }

    private static void GenerateGanttChart(StringBuilder sb, string title, IEnumerable<WorkItem> items)
    {
        sb.AppendLine("```mermaid");
        sb.AppendLine("gantt");
        sb.AppendLine($"    title {title}");
        sb.AppendLine("    dateFormat YYYY-MM-DD");

        // 获取所有工作项类型
        var features = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "Feature").ToList();
        var userStories = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "User Story").ToList();
        var tasks = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "Task").ToList();

        foreach (var feature in features)
        {
            var featureTitle = feature.Fields["System.Title"].ToString();
            sb.AppendLine($"    section {featureTitle}");

            // 显示 Feature 本身
            var featureStartDate = FormatDate(
                feature.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                    ? feature.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                    : null);
            var featureEndDate = FormatDate(
                feature.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                    ? feature.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                    : DateTime.Now.AddDays(30));
            var featureStatus = GetTaskStatus(feature.Fields["System.State"].ToString());
            
            sb.AppendLine($"    {featureTitle} :{featureStatus}, {featureStartDate}, {featureEndDate}");

            // 显示属于该 Feature 的 User Stories
            var featureStories = userStories.Where(s => 
                s.Fields.ContainsKey("System.Parent") && 
                s.Fields["System.Parent"].ToString() == feature.Id.ToString());

            foreach (var story in featureStories)
            {
                var storyTitle = story.Fields["System.Title"].ToString();
                var storyStartDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var storyEndDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(14));
                var storyStatus = GetTaskStatus(story.Fields["System.State"].ToString());
                
                sb.AppendLine($"    {storyTitle} :{storyStatus}, {storyStartDate}, {storyEndDate}");

                // 显示属于该 User Story 的 Tasks
                var storyTasks = tasks.Where(t => 
                    t.Fields.ContainsKey("System.Parent") && 
                    t.Fields["System.Parent"].ToString() == story.Id.ToString());

                foreach (var task in storyTasks)
                {
                    var taskTitle = task.Fields["System.Title"].ToString();
                    var startDate = FormatDate(
                        task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                            ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                            : null);
                    var endDate = FormatDate(
                        task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                            ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                            : DateTime.Now.AddDays(7));
                    var taskStatus = GetTaskStatus(task.Fields["System.State"].ToString());

                    sb.AppendLine($"    {taskTitle} :{taskStatus}, {startDate}, {endDate}");
                }
            }
        }

        // 显示没有父级 Feature 的 User Stories
        var orphanStories = userStories.Where(s => 
            !s.Fields.ContainsKey("System.Parent")).ToList();

        if (orphanStories.Any())
        {
            sb.AppendLine("    section 其他 User Stories");
            foreach (var story in orphanStories)
            {
                var storyTitle = story.Fields["System.Title"].ToString();
                var storyStartDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var storyEndDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(14));
                var storyStatus = GetTaskStatus(story.Fields["System.State"].ToString());
                
                sb.AppendLine($"    {storyTitle} :{storyStatus}, {storyStartDate}, {storyEndDate}");

                // 显示属于该 User Story 的 Tasks
                var storyTasks = tasks.Where(t => 
                    t.Fields.ContainsKey("System.Parent") && 
                    t.Fields["System.Parent"].ToString() == story.Id.ToString());

                foreach (var task in storyTasks)
                {
                    var taskTitle = task.Fields["System.Title"].ToString();
                    if (settings.ReportSettings?.DisplayOptions?.PrefixParentName ?? true)
                    {
                        taskTitle = $"{storyTitle} - {taskTitle}";
                    }

                    var startDate = FormatDate(
                        task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                            ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                            : null);
                    var endDate = FormatDate(
                        task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                            ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                            : DateTime.Now.AddDays(7));
                    var taskStatus = GetTaskStatus(task.Fields["System.State"].ToString());

                    sb.AppendLine($"    {taskTitle} :{taskStatus}, {startDate}, {endDate}");
                }
            }
        }

        // 显示没有父级的 Tasks
        var orphanTasks = tasks.Where(t => 
            !t.Fields.ContainsKey("System.Parent")).ToList();

        if (orphanTasks.Any())
        {
            sb.AppendLine("    section 其他任务");
            foreach (var task in orphanTasks)
            {
                var taskTitle = task.Fields["System.Title"].ToString();
                var startDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var endDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(7));
                var taskStatus = GetTaskStatus(task.Fields["System.State"].ToString());

                sb.AppendLine($"    {taskTitle} :{taskStatus}, {startDate}, {endDate}");
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();
    }

    private static void GenerateFeatureGanttChart(StringBuilder sb, string title, IEnumerable<WorkItem> items)
    {
        sb.AppendLine("```mermaid");
        sb.AppendLine("gantt");
        sb.AppendLine($"    title {title}");
        sb.AppendLine("    dateFormat YYYY-MM-DD");

        var features = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "Feature").ToList();
        var userStories = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "User Story").ToList();
        var tasks = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "Task").ToList();

        foreach (var feature in features)
        {
            var featureTitle = feature.Fields["System.Title"].ToString();
            sb.AppendLine($"    section {featureTitle}");

            // 显示 Feature 本身
            if (settings.ReportSettings?.DisplayOptions?.ShowFeatureInGantt ?? true)
            {
                var featureStartDate = FormatDate(
                    feature.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? feature.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var featureEndDate = FormatDate(
                    feature.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? feature.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(30));
                var featureStatus = GetTaskStatus(feature.Fields["System.State"].ToString());
                
                sb.AppendLine($"    {featureTitle} (Feature) :{featureStatus}, {featureStartDate}, {featureEndDate}");
            }

            // 显示属于该 Feature 的 User Stories
            var featureStories = userStories.Where(s => 
                s.Fields.ContainsKey("System.Parent") && 
                s.Fields["System.Parent"].ToString() == feature.Id.ToString());

            foreach (var story in featureStories)
            {
                var storyTitle = story.Fields["System.Title"].ToString();
                if (settings.ReportSettings?.DisplayOptions?.PrefixParentName ?? true)
                {
                    storyTitle = $"{featureTitle} - {storyTitle}";
                }

                var storyStartDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var storyEndDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(14));
                var storyStatus = GetTaskStatus(story.Fields["System.State"].ToString());
                
                sb.AppendLine($"    {storyTitle} :{storyStatus}, {storyStartDate}, {storyEndDate}");
            }
        }

        // 显示没有父级 Feature 的 User Stories
        var orphanStories = userStories.Where(s => 
            !s.Fields.ContainsKey("System.Parent")).ToList();

        if (orphanStories.Any())
        {
            sb.AppendLine("    section 其他 User Stories");
            foreach (var story in orphanStories)
            {
                var storyTitle = story.Fields["System.Title"].ToString();
                var storyStartDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var storyEndDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(14));
                var storyStatus = GetTaskStatus(story.Fields["System.State"].ToString());
                
                sb.AppendLine($"    {storyTitle} :{storyStatus}, {storyStartDate}, {storyEndDate}");

                // 显示属于该 User Story 的 Tasks
                var storyTasks = tasks.Where(t => 
                    t.Fields.ContainsKey("System.Parent") && 
                    t.Fields["System.Parent"].ToString() == story.Id.ToString());

                foreach (var task in storyTasks)
                {
                    var taskTitle = task.Fields["System.Title"].ToString();
                    if (settings.ReportSettings?.DisplayOptions?.PrefixParentName ?? true)
                    {
                        taskTitle = $"{storyTitle} - {taskTitle}";
                    }

                    var startDate = FormatDate(
                        task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                            ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                            : null);
                    var endDate = FormatDate(
                        task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                            ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                            : DateTime.Now.AddDays(7));
                    var taskStatus = GetTaskStatus(task.Fields["System.State"].ToString());

                    sb.AppendLine($"    {taskTitle} :{taskStatus}, {startDate}, {endDate}");
                }
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();
    }

    private static void GenerateUserStoryGanttChart(StringBuilder sb, string title, IEnumerable<WorkItem> items)
    {
        sb.AppendLine("```mermaid");
        sb.AppendLine("gantt");
        sb.AppendLine($"    title {title}");
        sb.AppendLine("    dateFormat YYYY-MM-DD");

        var userStories = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "User Story").ToList();
        var tasks = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "Task").ToList();

        foreach (var story in userStories)
        {
            var storyTitle = story.Fields["System.Title"].ToString();
            sb.AppendLine($"    section {storyTitle}");

            // 显示 User Story 本身
            if (settings.ReportSettings?.DisplayOptions?.ShowUserStoryInGantt ?? true)
            {
                var storyStartDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var storyEndDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(14));
                var storyStatus = GetTaskStatus(story.Fields["System.State"].ToString());
                
                sb.AppendLine($"    {storyTitle} (Story) :{storyStatus}, {storyStartDate}, {storyEndDate}");
            }

            // 显示属于该 User Story 的 Tasks
            var storyTasks = tasks.Where(t => 
                t.Fields.ContainsKey("System.Parent") && 
                t.Fields["System.Parent"].ToString() == story.Id.ToString());

            foreach (var task in storyTasks)
            {
                var taskTitle = task.Fields["System.Title"].ToString();
                if (settings.ReportSettings?.DisplayOptions?.PrefixParentName ?? true)
                {
                    taskTitle = $"{storyTitle} - {taskTitle}";
                }

                var startDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var endDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(7));
                var taskStatus = GetTaskStatus(task.Fields["System.State"].ToString());

                sb.AppendLine($"    {taskTitle} :{taskStatus}, {startDate}, {endDate}");
            }
        }

        // 显示没有父级的 Tasks
        var orphanTasks = tasks.Where(t => 
            !t.Fields.ContainsKey("System.Parent")).ToList();

        if (orphanTasks.Any())
        {
            sb.AppendLine("    section 其他任务");
            foreach (var task in orphanTasks)
            {
                var taskTitle = task.Fields["System.Title"].ToString();
                var startDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var endDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(7));
                var taskStatus = GetTaskStatus(task.Fields["System.State"].ToString());

                sb.AppendLine($"    {taskTitle} :{taskStatus}, {startDate}, {endDate}");
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();
    }

    private static IEnumerable<WorkItem> GetProjectWorkItems(WorkItemTrackingHttpClient witClient, ProjectConfig project)
    {
        try
        {
            var wiql = project.Query?.UseExistingQuery == true && !string.IsNullOrEmpty(project.Query.QueryPath)
                ? witClient.GetQueryAsync(project.ProjectName, project.Query.QueryPath).Result?.Wiql
                : CreateDefaultWiql(project.ProjectName).Query;

            var result = witClient.QueryByWiqlAsync(new Wiql { Query = wiql }).Result;
            var workItemIds = result.WorkItems.Select(item => item.Id).ToArray();

            if (workItemIds.Length > 0)
            {
                return witClient.GetWorkItemsAsync(workItemIds, expand: WorkItemExpand.All).Result;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取项目 {project.ProjectName} 的工作项发生错误：{ex.Message}");
        }

        return null;
    }

    private static void GenerateMergedReport(List<(WorkItem Item, string ProjectName)> allWorkItems, string tfsUrl, StringBuilder sb)
    {
        // 获取当前应使用的语言
        var currentLanguage = LanguageHelper.GetCurrentLanguage(settings.ReportSettings?.Language ?? "auto");
        var t = (string key) => LanguageResources.GetText(currentLanguage, key);

        // 1. 总体概览
        sb.AppendLine("## 总体概览");
        sb.AppendLine();
        sb.AppendLine($"- 工作项总数：{allWorkItems.Count}");
        sb.AppendLine($"- 完成项数量：{allWorkItems.Count(w => GetTaskStatus(w.Item.Fields["System.State"].ToString()) == "done")}");
        sb.AppendLine($"- 进行中数量：{allWorkItems.Count(w => GetTaskStatus(w.Item.Fields["System.State"].ToString()) == "active")}");
        sb.AppendLine();

        // 2. 按项目统计
        sb.AppendLine("## 项目统计");
        foreach (var projectGroup in allWorkItems.GroupBy(w => w.ProjectName))
        {
            sb.AppendLine($"### {projectGroup.Key}");
            sb.AppendLine($"- 工作项数量：{projectGroup.Count()}");
            sb.AppendLine($"- 完成项数量：{projectGroup.Count(w => GetTaskStatus(w.Item.Fields["System.State"].ToString()) == "done")}");
            sb.AppendLine($"- 进行中数量：{projectGroup.Count(w => GetTaskStatus(w.Item.Fields["System.State"].ToString()) == "active")}");
            sb.AppendLine();
        }

        // 3. 总体甘特图
        sb.AppendLine("## 总体甘特图");
        
        // 3.1 Feature 视图
        sb.AppendLine("### Feature 层级视图");
        GenerateFeatureGanttChart(sb, "所有项目 Feature 进度", allWorkItems.Select(w => w.Item));

        // 3.2 User Story 视图
        sb.AppendLine("### User Story 层级视图");
        GenerateUserStoryGanttChart(sb, "所有项目 User Story 进度", allWorkItems.Select(w => w.Item));

        // 4. 工作项分类
        sb.AppendLine("## 工作项分类");
        
        // 4.1 Features
        var features = allWorkItems.Where(w => 
            w.Item.Fields["System.WorkItemType"].ToString() == "Feature");
        if (features.Any())
        {
            sb.AppendLine("### Features");
            sb.AppendLine("| 项目 | ID | 标题 | 状态 | 负责人 | 开始日期 | 结束日期 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var (item, projectName) in features)
            {
                var id = item.Id;
                var title = item.Fields["System.Title"].ToString();
                var state = item.Fields["System.State"].ToString();
                var assignedTo = GetPersonName(
                    item.Fields.ContainsKey("System.AssignedTo") 
                        ? item.Fields["System.AssignedTo"] 
                        : null);
                var startDate = FormatDate(
                    item.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? item.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var endDate = FormatDate(
                    item.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? item.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(30));

                sb.AppendLine($"| {projectName} | {id} | {title} | {state} | {assignedTo} | {startDate} | {endDate} |");
            }
            sb.AppendLine();
        }

        // 4.2 User Stories
        var userStories = allWorkItems.Where(w => 
            w.Item.Fields["System.WorkItemType"].ToString() == "User Story");
        if (userStories.Any())
        {
            sb.AppendLine("### User Stories");
            sb.AppendLine("| 项目 | ID | 标题 | 状态 | 负责人 | 开始日期 | 结束日期 | 所属 Feature |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var (item, projectName) in userStories)
            {
                var id = item.Id;
                var title = item.Fields["System.Title"].ToString();
                var state = item.Fields["System.State"].ToString();
                var assignedTo = GetPersonName(
                    item.Fields.ContainsKey("System.AssignedTo") 
                        ? item.Fields["System.AssignedTo"] 
                        : null);
                var startDate = FormatDate(
                    item.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? item.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var endDate = FormatDate(
                    item.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? item.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(14));
                var parentId = item.Fields.ContainsKey("System.Parent") 
                    ? item.Fields["System.Parent"].ToString() 
                    : "-";

                sb.AppendLine($"| {projectName} | {id} | {title} | {state} | {assignedTo} | {startDate} | {endDate} | {parentId} |");
            }
            sb.AppendLine();
        }

        // 4.3 Tasks
        var tasks = allWorkItems.Where(w => 
            w.Item.Fields["System.WorkItemType"].ToString() == "Task");
        if (tasks.Any())
        {
            sb.AppendLine("### Tasks");
            sb.AppendLine("| 项目 | ID | 标题 | 状态 | 负责人 | 开始日期 | 结束日期 | 所属 Story |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var (item, projectName) in tasks)
            {
                var id = item.Id;
                var title = item.Fields["System.Title"].ToString();
                var state = item.Fields["System.State"].ToString();
                var assignedTo = GetPersonName(
                    item.Fields.ContainsKey("System.AssignedTo") 
                        ? item.Fields["System.AssignedTo"] 
                        : null);
                var startDate = FormatDate(
                    item.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? item.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var endDate = FormatDate(
                    item.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? item.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(7));
                var parentId = item.Fields.ContainsKey("System.Parent") 
                    ? item.Fields["System.Parent"].ToString() 
                    : "-";

                sb.AppendLine($"| {projectName} | {id} | {title} | {state} | {assignedTo} | {startDate} | {endDate} | {parentId} |");
            }
            sb.AppendLine();
        }

        // 5. 按人员分组的视图
        sb.AppendLine("## 团队成员任务分配");
        var workItemsByPerson = allWorkItems
            .GroupBy(w => GetPersonName(
                w.Item.Fields.ContainsKey("System.AssignedTo") 
                    ? w.Item.Fields["System.AssignedTo"] 
                    : null));

        foreach (var personGroup in workItemsByPerson)
        {
            sb.AppendLine($"### {personGroup.Key}");
            sb.AppendLine();

            // 个人甘特图
            sb.AppendLine("#### 个人甘特图");
            var personItems = personGroup.Select(w => w.Item);
            var hasFeatures = personItems.Any(i => i.Fields["System.WorkItemType"].ToString() == "Feature");
            if (hasFeatures)
            {
                GenerateFeatureGanttChart(sb, $"{personGroup.Key}的进度甘特图", personItems);
            }
            else
            {
                GenerateUserStoryGanttChart(sb, $"{personGroup.Key}的进度特图", personItems);
            }

            // 个人工作项列表
            sb.AppendLine("#### 工作项列表");
            sb.AppendLine();
            sb.AppendLine("| 项目 | ID | 类型 | 标题 | 状态 | 开始日期 | 结束日期 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");

            foreach (var (item, projectName) in personGroup)
            {
                var id = item.Id;
                var type = item.Fields["System.WorkItemType"].ToString();
                var title = item.Fields["System.Title"].ToString();
                var state = item.Fields["System.State"].ToString();
                var startDate = FormatDate(
                    item.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? item.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var endDate = FormatDate(
                    item.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? item.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(7));

                sb.AppendLine($"| {projectName} | {id} | {type} | {title} | {state} | {startDate} | {endDate} |");
            }
            
            sb.AppendLine();
        }
    }

    // 添加配置验证方法
    private static string ValidateSettings(AppSettings settings)
    {
        if (settings == null)
            return "无法解析配置文件";

        if (string.IsNullOrWhiteSpace(settings.TfsUrl))
            return "缺少 TfsUrl 配置";

        // 规范化 URL
        settings.TfsUrl = NormalizeUrl(settings.TfsUrl);

        if (string.IsNullOrWhiteSpace(settings.PersonalAccessToken))
            return "缺少 PersonalAccessToken 配置";

        if (settings.Projects == null || !settings.Projects.Any())
            return "缺少 Projects 配置";

        foreach (var project in settings.Projects)
        {
            if (string.IsNullOrWhiteSpace(project.ProjectName))
                return "存在项目名称为空的配置";
        }

        return null;
    }

    // 添加 URL 规范化方法
    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        // 移除末尾的斜杠
        url = url.TrimEnd('/');

        // 如果没有协议前缀，添加 https://
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        return url;
    }

    // 添加配置示例生成方法
    private static string GetConfigExample()
    {
        var example = new AppSettings
        {
            TfsUrl = "https://dev.azure.com/你的组织名",
            PersonalAccessToken = "你的 PAT 令牌",
            ReportSettings = new ReportSettings
            {
                MergeProjects = true,
                MergedTitle = "多项目整合报告",
                Language = "auto"
            },
            Projects = new List<ProjectConfig>
            {
                new ProjectConfig
                {
                    ProjectName = "项目名称",
                    Query = new QuerySettings
                    {
                        UseExistingQuery = false,
                        QueryPath = "Shared Queries/查询路径",
                        CustomWiql = null
                    },
                    FieldMappings = new WorkItemFieldMappings
                    {
                        Feature = new WorkItemTypeFields
                        {
                            StartDateField = "Custom.FeatureStartDate",
                            EndDateField = "Custom.FeatureEndDate"
                        },
                        UserStory = new WorkItemTypeFields
                        {
                            StartDateField = "Custom.StoryStartDate",
                            EndDateField = "Custom.StoryEndDate"
                        },
                        Task = new WorkItemTypeFields
                        {
                            StartDateField = "Custom.TaskStartDate",
                            EndDateField = "Custom.TaskEndDate"
                        }
                    }
                }
            }
        };

        return JsonConvert.SerializeObject(example, Formatting.Indented);
    }

    private static string GetStartDate(WorkItem workItem, ProjectConfig projectConfig)
    {
        var workItemType = workItem.Fields["System.WorkItemType"].ToString();
        var fieldMapping = workItemType switch
        {
            "Feature" => projectConfig.FieldMappings.Feature,
            "User Story" => projectConfig.FieldMappings.UserStory,
            "Task" => projectConfig.FieldMappings.Task,
            _ => null
        };

        if (fieldMapping == null || string.IsNullOrEmpty(fieldMapping.StartDateField))
            return FormatDate(null);

        return FormatDate(
            workItem.Fields.ContainsKey(fieldMapping.StartDateField)
                ? workItem.Fields[fieldMapping.StartDateField]
                : null);
    }

    private static string GetEndDate(WorkItem workItem, ProjectConfig projectConfig)
    {
        var workItemType = workItem.Fields["System.WorkItemType"].ToString();
        var fieldMapping = workItemType switch
        {
            "Feature" => projectConfig.FieldMappings.Feature,
            "User Story" => projectConfig.FieldMappings.UserStory,
            "Task" => projectConfig.FieldMappings.Task,
            _ => null
        };

        if (fieldMapping == null || string.IsNullOrEmpty(fieldMapping.EndDateField))
            return FormatDate(DateTime.Now.AddDays(workItemType switch
            {
                "Feature" => 30,
                "User Story" => 14,
                "Task" => 7,
                _ => 7
            }));

        return FormatDate(
            workItem.Fields.ContainsKey(fieldMapping.EndDateField)
                ? workItem.Fields[fieldMapping.EndDateField]
                : null);
    }
}
