# Multi-stage build for the Storefront API (linux/amd64).
# Stage 1: build & publish with the .NET 10 SDK.
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY storefront-api.csproj ./
RUN dotnet restore storefront-api.csproj

COPY . ./
RUN dotnet publish storefront-api.csproj -c Release -o /app --no-restore

# Stage 2: minimal ASP.NET runtime.
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Listen on 8080 (non-privileged) so the container runs as an arbitrary UID.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Group 0 with group-writable perms keeps the image OpenShift-friendly
# (arbitrary UID, gid 0). No fixed USER so any UID can run it.
RUN chgrp -R 0 /app && chmod -R g=u /app

ENTRYPOINT ["dotnet", "storefront-api.dll"]
