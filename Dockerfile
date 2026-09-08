# syntax=docker/dockerfile:1

# ---- Stage 1: build the React designer ----
FROM node:20-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json* ./
RUN npm install
COPY web/ ./
RUN npm run build

# ---- Stage 2: build the API ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props JetReportDesigner.slnx ./
COPY src/ ./src/
RUN dotnet restore src/JetReportDesigner.Api/JetReportDesigner.Api.csproj
RUN dotnet publish src/JetReportDesigner.Api/JetReportDesigner.Api.csproj -c Release -o /app --no-restore

# ---- Stage 3: runtime (API serving the SPA from wwwroot) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# fonts-liberation: metric-compatible fonts for the PDF renderer (PdfSharp ships none).
# libfontconfig1: needed by SkiaSharp if the QuestPDF fallback renderer is ever used.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-liberation libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "JetReportDesigner.Api.dll"]
