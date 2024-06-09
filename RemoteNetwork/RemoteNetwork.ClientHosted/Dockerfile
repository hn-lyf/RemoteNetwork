FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime \
    && echo 'Asia/Shanghai' >/etc/timezone
RUN apt-get update && apt install -y --no-install-recommends ca-certificates iproute2
WORKDIR /app
COPY //publish .
ENV enableDistribution=false
LABEL appName="远程网络"
LABEL update="1"
ENTRYPOINT ["dotnet","RemoteNetwork.ClientHosted.dll"]