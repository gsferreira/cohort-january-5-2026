# Workshop Step 011: Azure AI Setup

## Mission 🎯

**Important:** Azure has been renaming Azure AI services and the portal. If you can't find an option, please ask for help. 

In this step, you'll set up Azure OpenAI resources through the Azure portal. This foundation will enable AI-powered transaction enhancement in the next steps.

**Your goal**: Create and configure an Azure OpenAI resource with proper deployment settings for the workshop.

**Learning Objectives**:
- Azure portal navigation and resource creation
- Azure OpenAI service configuration
- Model deployment and testing

---

## Prerequisites

Before starting, ensure you have:
- **Azure Account**
- Completed previous workshop steps (001-005)
- **Starting point**: Use the `checkpoints/01-week-end` folder as your baseline

---

## Step 11.1: Create Azure OpenAI Resource

*Navigate to the Azure portal and create a new OpenAI resource.*

### 11.1.1: Access Azure Portal

1. **Go to Azure Portal**: https://portal.azure.com
2. **Sign in** with your Azure account credentials
3. **Navigate to Create a resource** (+ icon in the top-left)

### 11.1.2: Search for Microsoft Foundry

1. **Search for "Microsoft Foundry"** in the marketplace
2. **Select "Microsoft Foundry"** from the results
3. **Click "Create"** to start the setup process

### 11.1.3: Configure Basic Settings

Fill in the resource creation form:

**Project Details:**
- **Subscription**: Select your Azure subscription
- **Resource Group**: Create new or use existing (e.g., "budget-tracker-rg")

**Instance Details:**
- **Region**: Choose a region that supports OpenAI (Foundry Models) (e.g., West Europe)
- **Name**: Enter a unique name (e.g., "budget-tracker-ai-[yourname]")

**Click "Next: Network"** to continue.

### 11.1.4: Network Configuration

For workshop purposes, use the default settings:
- **Network Access**: "All networks, including the internet, can access this resource"

**Note**: In production, you'd want to restrict network access.

**Click "Next: Tags"** to continue.

### 11.1.5: Add Tags (Optional)

Add tags for organization:
- **Environment**: "development"
- **Project**: "budget-tracker-workshop"
- **Owner**: Your name or team

**Click "Next: Review + create"** to continue.

### 11.1.6: Review and Create

1. **Review your configuration**
2. **Click "Create"** to deploy the resource
3. **Wait for deployment** (usually takes 2-3 minutes)
4. **Click "Go to resource"** when deployment completes

---

## Step 11.2: Deploy AI Model

*Create a model deployment that your application will use.*

### 11.2.1: Navigate to Model Deployments
1. In your Foundry resource, follow the link to the Foundry Portal
2. Click **"Models + endpoints"** in the left sidebar
3. Click **"Deploy model"**
4. Click **"Deploy base model"**

### 11.2.2: Configure Model Deployment

**Deployment Settings:**
- **Model**: Select "gpt-4.1-mini" (recommended)
- **Model version**: Use the default latest version
- **Deployment name**: Enter "gpt-4.1-mini" (remember this name!)
- **Content filter**: Use default settings
- **Deployment type**: Standard
- **Tokens per minute rate limit**: Use default settings

**Click "Deploy"** to deploy the model.

### 11.2.3: Verify Deployment

1. **Wait for deployment** to complete (1-2 minutes)
2. **Verify status** shows as "Succeeded"
3. **Note the deployment name** - you'll need this in your code

---

## Step 11.3: Get API Credentials

*Collect the endpoint and API keys needed for your application. Available at the Home of your Microsoft Foundry project. The endpoint we are looking for is the Azure AI Services endpoint.*

**Security Note**: Never commit API keys to version control!

---

## Step 11.4: Test Azure OpenAI Access

*Verify your setup works using the Foundry Studio.*


### 11.4.1: Test Chat Completion

1. Navigate to **"Models + endpoints"** in the left sidebar
2. **Go to your deployment** 
3. **Open the playground** 
4. **Test with a simple prompt**:
   ```
   Transform this bank transaction: "AMZN MKTP US*123456789" into a readable description.
   ```
5. **Verify you get a response** like _"The bank transaction "AMZN MKTP US*123456789" can be described as a purchase made through Amazon Marketplace in the United States, with the transaction possibly linked to a specific order or account identified by the number 123456789."_

If this works, your Azure OpenAI setup is complete!

---

## Troubleshooting 🔧

### Common Issues

**"Access Denied" when creating OpenAI resource:**
- Ensure you have Azure OpenAI access approval
- Try a different region (East US, West Europe typically work)
- Check your Azure subscription has sufficient permissions

**"Deployment failed" for model:**
- Verify the model is available in your chosen region
- Try reducing the token rate limit
- Ensure you have sufficient quota

---

## Summary ✅

You've successfully set up:

✅ **Azure OpenAI Resource**: Created and configured in Azure portal
✅ **Model Deployment**: gpt-4.1-mini deployed and ready
✅ **API Credentials**: Endpoint and API key collected securely
✅ **Tested Access**: Verified the setup works via Azure AI Foundry

**Next Step**: Move to [012-ai-transaction-enhancement-backend.md](021-ai-transaction-enhancement-backend.md) to configure your local environment and implement the AI service infrastructure.

---

## Additional Resources

- **Azure AI Foundry documentation**: https://learn.microsoft.com/en-gb/azure/ai-foundry/
- **Azure AI Foundry**: https://ai.azure.com/
- **Pricing Calculator**: https://azure.microsoft.com/pricing/calculator/
- **Azure products by region**: https://azure.microsoft.com/en-gb/explore/global-infrastructure/products-by-region/table