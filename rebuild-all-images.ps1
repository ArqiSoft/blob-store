docker build -t leanda/blob-storage-webapi:latest -f Sds.Storage.Blob.WebApi/Dockerfile .
docker build -t leanda/blob-storage-webapi-integration:latest -f Sds.Storage.Blob.WebApi.Tests/Dockerfile .
docker image ls leanda/blob-storage-*