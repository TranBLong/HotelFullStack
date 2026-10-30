# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ENV DOTNET_SYSTEM_NET_DISABLEIPV6=1
WORKDIR /src

COPY . .
WORKDIR /src/HotelBlazor
RUN dotnet publish "HotelBlazor.csproj" -c Release -o /app/publish

# Thêm 2 dòng kiểm tra theo yêu cầu của bạn vào đây:
RUN dotnet --version
RUN ls /root/.nuget/packages | grep -i "internal.assets" ; ls /app/publish/wwwroot

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet HotelBlazor.dll"]