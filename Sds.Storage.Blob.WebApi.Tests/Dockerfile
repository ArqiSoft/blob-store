FROM microsoft/dotnet:2.1-sdk AS build-env

ARG RID=linux-x64

WORKDIR /build

COPY Sds.Storage.Blob.Domain/Sds.Storage.Blob.Domain.csproj Sds.Storage.Blob.Domain/
COPY Sds.Storage.Blob.WebApi.Tests/Sds.Storage.Blob.WebApi.Tests.csproj Sds.Storage.Blob.WebApi.Tests/
COPY Nuget.config .

RUN dotnet restore --configfile Nuget.config Sds.Storage.Blob.WebApi.Tests/Sds.Storage.Blob.WebApi.Tests.csproj

COPY Sds.Storage.Blob.Domain Sds.Storage.Blob.Domain
COPY Sds.Storage.Blob.WebApi.Tests Sds.Storage.Blob.WebApi.Tests

RUN dotnet publish Sds.Storage.Blob.WebApi.Tests/Sds.Storage.Blob.WebApi.Tests.csproj -r $RID -c Release -o /dist

# Build runtime image
FROM microsoft/dotnet:2.1-sdk

LABEL maintainer="pshenichnov@gmail.com"

WORKDIR /app

RUN apt-get update && apt-get install -y curl
RUN curl https://raw.githubusercontent.com/vishnubob/wait-for-it/master/wait-for-it.sh > /app/wait-for-it.sh && chmod 777 /app/wait-for-it.sh

COPY --from=build-env /dist ./

ENTRYPOINT ["dotnet", "vstest", "./Sds.Storage.Blob.WebApi.Tests.dll"]
