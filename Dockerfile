# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["HotelBlazor/HotelBlazor.csproj", "HotelBlazor/"]
RUN dotnet restore "HotelBlazor/HotelBlazor.csproj"

COPY . .
WORKDIR /src/HotelBlazor
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
# Đọc PORT lúc chạy (Render cấp PORT), mặc định 8080 khi chạy local
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet HotelBlazor.dll"]