# syntax=docker/dockerfile:1
# The library is not on NuGet yet and the sample references the sister folder ..\BancontactPro.Net, so the build context is the
# folder that holds both repositories. From the sample's folder: docker compose up --build (see docker-compose.yml).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Same layout as on disk, so the project reference resolves: /src/BancontactPro.Net and /src/BancontactPro.SDK.Samples.Net.
COPY BancontactPro.Net/src/BancontactPro.Net/BancontactPro.Net.csproj BancontactPro.Net/src/BancontactPro.Net/
COPY BancontactPro.SDK.Samples.Net/samples/BancontactPro.Net.Sample.Web/BancontactPro.Net.Sample.Web.csproj BancontactPro.SDK.Samples.Net/samples/BancontactPro.Net.Sample.Web/
RUN dotnet restore BancontactPro.SDK.Samples.Net/samples/BancontactPro.Net.Sample.Web/BancontactPro.Net.Sample.Web.csproj

COPY BancontactPro.Net/src/ BancontactPro.Net/src/
COPY BancontactPro.SDK.Samples.Net/samples/BancontactPro.Net.Sample.Web/ BancontactPro.SDK.Samples.Net/samples/BancontactPro.Net.Sample.Web/
# MinVer reads git tags, which the image build does not have; the sample's own version is irrelevant.
RUN dotnet publish BancontactPro.SDK.Samples.Net/samples/BancontactPro.Net.Sample.Web/BancontactPro.Net.Sample.Web.csproj \
    -c Release --no-restore -o /app /p:MinVerSkip=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The .NET image's non-root user.
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BancontactPro.Net.Sample.Web.dll"]
