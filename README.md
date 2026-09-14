# CoffeeNChill – Part 1

Azure Functions API for CoffeeNChill menu management and centralized staff documents. Menu entities are stored in Azure Table Storage. Staff documents use Azure Files in rubric-compliant mode, with an Azurite Blob adapter for local container demonstrations because Azurite does not emulate Azure Files.

## Implemented endpoints

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/menu` | Create a menu item |
| GET | `/api/menu` | List all menu items |
| GET | `/api/menu/category/{category}` | Filter menu items by category |
| PUT | `/api/menu/{category}/{id}` | Update price and/or availability |
| DELETE | `/api/menu/{category}/{id}` | Delete a menu item |
| POST | `/api/documents/upload` | Upload multipart field `file` |
| GET | `/api/documents` | List staff documents and metadata |
| GET | `/api/documents/download/{fileName}` | Download a document |
| DELETE | `/api/documents/{fileName}` | Delete a document (additional endpoint) |

## Prerequisites

- .NET 10 SDK
- Azure Functions Core Tools v4
- Docker Desktop or Docker Engine
- Postman
- C#
- Azure Table Storage
- Azure File Storage
- GitHub

## Run locally

1. Start Azurite:

   ```bash
   docker run --name coffeenchill-azurite -d \
     -p 10000:10000 -p 10001:10001 -p 10002:10002 \
     mcr.microsoft.com/azure-storage/azurite
   ```

2. Create the local settings file:

   ```bash
   cd CoffeeNChill.Functions/CoffeeNChill.Functions
   cp local.settings.example.json local.settings.json
   ```

   On Windows PowerShell use:

   ```powershell
   Copy-Item local.settings.example.json local.settings.json
   ```

3. Start the Functions host:

   ```bash
   func start --port 7071
   ```

`UseDevelopmentStorage=true` connects the host process to Azurite on localhost. `local.settings.json` is intentionally ignored by Git.

## Standalone Docker execution

No Docker Compose is required.

1. Create a network and run the tagged Azurite container:

   ```bash
   docker network create coffeenchill-network
   docker pull mcr.microsoft.com/azure-storage/azurite
   docker tag mcr.microsoft.com/azure-storage/azurite coffeenchill-azurite:v1.0
   docker run --name azurite --network coffeenchill-network -d \
     -p 10000:10000 -p 10001:10001 -p 10002:10002 \
     coffeenchill-azurite:v1.0
   ```

2. Build the Functions image from the Functions project directory:

   ```bash
   cd CoffeeNChill.Functions/CoffeeNChill.Functions
   docker build -t <dockerhub_username>/coffeenchill-functions:v1.0 .
   ```

3. Run the Functions container on the same network:

   ```bash
   docker run --name coffeenchill-functions --network coffeenchill-network \
     -p 7071:80 \
     -e FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \
     -e DocumentStorageMode=Blob \
     -e 'AzureWebJobsStorage=DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;QueueEndpoint=http://azurite:10001/devstoreaccount1;TableEndpoint=http://azurite:10002/devstoreaccount1;' \
     <dockerhub_username>/coffeenchill-functions:v1.0
   ```

Inside a container, `UseDevelopmentStorage=true` would point to that same container rather than the separate Azurite container. The explicit connection string above therefore uses the Azurite container hostname.

## Azure File Share mode

Azurite supports Blob, Queue and Table services, but not Azure Files. To run the document API against the required real Azure File Share, set:

```text
DocumentStorageMode=FileShare
DocumentsConnectionString=<real Azure Storage connection string>
```

The application creates the `staff-docs` share automatically. For the isolated local Docker demonstration, `DocumentStorageMode=Blob` provides the same document API using the Azurite `staff-docs` blob container.

## Docker Hub publishing

```bash
docker login
docker push <dockerhub_username>/coffeenchill-functions:v1.0
docker tag coffeenchill-azurite:v1.0 <dockerhub_username>/coffeenchill-azurite:v1.0
docker push <dockerhub_username>/coffeenchill-azurite:v1.0
```

Replace `<dockerhub_username>` and add the public image URLs before submission:

- Functions image: **TODO – add public Docker Hub URL**
- Azurite image: **TODO – add public Docker Hub URL**

## Postman tests

Import both files from `/docs`:

- `CoffeeNChill-Part1.postman_collection.json`
- `CoffeeNChill-Part1.postman_environment.json`

Select **CoffeeNChill Local** and run the collection in order. The upload request uses `/docs/sample-staff-document.txt`; reselect that file in Postman if its imported path is not resolved automatically.

## Group contributions

Current Git history contains five commits attributed to `KgaoT`. Add every member below and ensure Git history contains at least five meaningful commits from each person.

| Group member | Student number | Contribution |
|---|---|---|
| Kgaogelo Tladi | ST10403879 | Initial project structure and staff-document foundation |
| TODO | TODO | TODO |

## Demonstration video

YouTube: **TODO – add the final public or unlisted video URL**

The video must show both standalone containers, `docker ps`, the complete successful Postman run, and the stored menu/document data.

## Important Azurite ports

| Port | Service |
|---|---|
| 10000 | Blob |
| 10001 | Queue |
| 10002 | Table |
