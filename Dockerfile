# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Stage 1: Clone repository from GitHub
FROM alpine/git AS clone
WORKDIR /repo
RUN git clone https://github.com/tdankers/UserMetaApi.git .

# Stage 2: Build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY --from=clone /repo .
WORKDIR "/src/UserMetaApi"
RUN dotnet restore "./UserMetaApi.csproj"
RUN dotnet build "./UserMetaApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish the service project
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./UserMetaApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 80
EXPOSE 443
ENTRYPOINT ["dotnet", "UserMetaApi.dll"]