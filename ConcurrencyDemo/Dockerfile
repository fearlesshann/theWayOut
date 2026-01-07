# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ConcurrencyDemo.csproj", "./"]
RUN dotnet restore "ConcurrencyDemo.csproj"
COPY . .
RUN dotnet publish "ConcurrencyDemo.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# 暴露端口 (ASP.NET Core 8.0 默认端口是 8080)
EXPOSE 8080

# 设置时区
ENV TZ=Asia/Shanghai

ENTRYPOINT ["dotnet", "ConcurrencyDemo.dll"]
