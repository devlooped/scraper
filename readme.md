# scraper

A general purpose web scraper API built on Azure Container Apps

## How to clone and deploy

Instructions use the [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/) exclusively,
since that gives you the most control over the entire deployment.

1. **Resource Group**: make sure you are using the right account/subscription from the CLI.
    1. Show currently selected account: 
       ```
       az account show
       ```
    1. If you want to change the subscription: 
       ```
       az account set --subscription <subscription-id>
       ``` 
       To lookup the subscription id:
       ```
       az account list --output table
       ```
    3. Create resource group: 
       ```
       az group create --name scraper --location <location>
       ```
       To list available locations: 
       ```
       az account list-locations --output table
       ```
    5. Ensure CLI extensions install automatically:
       ```
       az config set extension.use_dynamic_install=yes_without_prompt
       ```

2. **Log Analytics**: setup a new log analytics workspace for logs from the app.
    1. Create it: 
       ```
       az monitor log-analytics workspace create -g scraper -n scraper
       ``` 
       Copy the `customerId` value from the returned payload.
    2. Get shared key: 
       ```
       az monitor log-analytics workspace get-shared-keys -g scraper -n scraper
       ```

3. **Container App Environment**: create and configure the app environment with the logs workspace created above.
    1. List available locations for container app environments: 
       ```
       az provider show -n Microsoft.App --query "resourceTypes[?resourceType=='managedEnvironments'].locations"
       ```
    2. Create with: 
       ```
       az containerapp env create -g scraper -n scraper --logs-workspace-id [customerId] --logs-workspace-key [sharedKey] --location <location>
       ```

4. **Container Registry**: deployments to an app come from images in a container registry, which 
   is populated automatically from CI.
    1. Create with: 
       ```
       az acr create -g scraper -n scraper --sku Basic --location <location>
       ```
       It's probably a good idea to use the same location as the app environment.
       Note the `loginServer` value in the returned payload.
    2. Login to it with: 
       ```
       az acr login -n scraper
       ```
    3. Enable admin mode (so we can get the passwords) with: 
       ```
       az acr update -n scraper --admin-enabled true
       ```
    4. Retrieve the username/password for the registry with: 
       ```
       az acr credential show -n scraper
       ```

5. **Container App**: finally!
    1. Create with: 
       ```
       az containerapp create -g scraper -n scraper --environment scraper
       ```
    2. Enable HTTP ingress with: 
       ```
       az containerapp ingress enable -g scraper -n scraper --type external --allow-insecure --target-port 80
       ```
       Note: we don't really need HTTPS since Azure will automatically provide a proper HTTPS endpoint. 
    3. Set the container registry to use: 
       ```
       az containerapp registry set -g scraper -n scraper --server <loginServer> --username scraper --password <password>
       ```
       Note the `--server` argument is the `loginServer` from the previous section.

6. **GitHub**: on to the setup on the repo side.
    1. Clone the repo
    2. Create an Actions repository secret named `AZURE_CONTAINER_PWD` with the container registry password from step `4.4`.
    3. Create credentials to update the resource group from CI:
       ```
       az ad sp create-for-rbac --name scraper --role contributor --scopes "/subscriptions/<subscription>/resourceGroups/scraper" --sdk-auth
       ```
       Copy the entire response payload.
    4. Create an Actions repository secret named `AZURE_CREDENTIALS` with the copied value.
    5. If you changed resource names, update the [build.yml](.github/workflows/build.yml) file accordingly.

Now you can run the `build` workflow and see it live!