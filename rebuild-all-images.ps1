docker build -t docker.your-company.com/blob-storage-webapi:ci-local -f Sds.Storage.Blob.WebApi/Dockerfile .
docker build -t docker.your-company.com/blob-storage-webapi-tests:ci-local -f Sds.Storage.Blob.WebApi.Tests/Dockerfile .
docker image ls docker.your-company.com/blob-storage-*