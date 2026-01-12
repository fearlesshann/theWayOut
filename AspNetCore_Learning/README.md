# ASP.NET Core 学习之旅

这是一个全新的 ASP.NET Core Web API 项目，用于开始您的学习之旅。

## 项目结构

*   `Program.cs`: 应用的入口点，包含依赖注入 (DI) 容器的配置和 HTTP 请求管道 (Middleware) 的设置。这是 ASP.NET Core 应用的心脏。
*   `appsettings.json`: 配置文件。
*   `AspNetCore_Learning.csproj`: 项目文件，定义了使用的 .NET 版本和依赖包。
*   `Controllers`: (如果使用控制器模式) 存放 API 控制器。
*   `WeatherForecast.cs`: 示例数据模型。

## 学习路线建议

1.  **理解 Minimal API vs Controllers**:
    *   当前模板默认生成的代码在 `Program.cs` 中使用了 Minimal API 风格（直接在 `app.MapGet` 中写逻辑）。
    *   您可以尝试将其重构为 Controller 风格，以了解两种模式的区别。

2.  **依赖注入 (Dependency Injection)**:
    *   尝试创建一个服务（例如 `IWeatherService`），并在 `Program.cs` 中注册它，然后在 API 中注入使用。

3.  **中间件 (Middleware)**:
    *   了解 `app.Use...` 系列方法的执行顺序。
    *   尝试编写一个自定义中间件（例如记录请求耗时）。

4.  **配置与环境**:
    *   学习如何从 `appsettings.json` 读取配置。
    *   了解 Development 和 Production 环境的区别。

5.  **Entity Framework Core**:
    *   集成数据库，实现真正的增删改查 (CRUD)。

## 如何运行

在终端中执行：

```bash
dotnet run
```

然后访问控制台输出的 URL (通常是 `http://localhost:5xxx/weatherforecast`) 查看结果。
