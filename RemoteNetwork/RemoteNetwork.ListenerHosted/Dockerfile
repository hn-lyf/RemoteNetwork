FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime \
    && echo 'Asia/Shanghai' >/etc/timezone
WORKDIR /app
COPY //publish .
ENV enableDistribution=false
LABEL appName="远程组网服务器"
LABEL update="1"
LABEL appconfig="/app/appsettings.json"
ENTRYPOINT ["dotnet","RemoteNetwork.ListenerHosted.dll"]