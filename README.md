# 🦜️🔗 LangChain Chat
![preview](images/preview.png)

This project shows how to make a chat with LLM usgin [LangChain](https://github.com/tryAGI/LangChain) and Blazor.

# Features
- chat with any local or paid model
- have multiple models with any chain configurations(agents, RAG, custom tools, etc...)
- conversation history and multiple conversations
- automatic conversation name generation
- code syntax higlight

# Installation

Clone the project:

```
git clone https://github.com/TesAnti/LangChainChat.git
```

You can run this chat against any model served locally or remotely, as long as it exposes an OpenAI-compatible API. This includes [Ollama](https://ollama.com), [vLLM](https://docs.vllm.ai), [LiteLLM](https://docs.litellm.ai), and any other compatible provider.

## Running with a local model

Install and start [Ollama](https://ollama.com), then pull a model:

```
ollama pull mistral:latest
```

The default configuration in `appsettings.json` points to `http://localhost:11434/v1`, so it will work out of the box.

## Running with a remote model

Point the `DefaultModel` and/or add entries under `ExternalModels` in `appsettings.json` to any remote OpenAI-compatible endpoint (vLLM, LiteLLM, etc.):

```json
{
  "DefaultModel": {
    "Name": "My Remote Model",
    "Endpoint": "https://my-llm-server.example.com/v1",
    "ModelId": "my-model-id",
    "ApiKey": "my-api-key",
    "Temperature": 0.0,
    "Stop": [ "User:" ]
  },
  "ExternalModels": [
    {
      "Name": "vLLM - Mistral",
      "Endpoint": "https://vllm.example.com/v1",
      "ModelId": "mistralai/Mistral-7B-Instruct-v0.1",
      "ApiKey": "EMPTY",
      "Temperature": 0.0,
      "Stop": [ "User:" ]
    }
  ]
}
```

`DefaultModel` is used for the name generator and as the primary chat model. Each entry in `ExternalModels` is registered as an additional selectable model in the chat UI.

# Configuration

Models are configured entirely through `appsettings.json`. You can also customize the chain logic in `LangChainConfigExtensions.cs` to add agents, RAG, custom tools, or any other [LangChain](https://github.com/tryAGI/LangChain) chain configuration.

# Deploying on OpenShift

You can deploy LangChainChat on OpenShift using S2I binary builds.

1. Create the build configuration:
```
oc new-build --name=langchain-chat dotnet:8.0-ubi8 --binary=true
```

2. Build and publish the application:
```
dotnet build && dotnet publish
```

3. Start the build from the published output:
```
oc start-build langchain-chat --from-dir=bin/Release/net8.0/publish
```

4. Expose the application with a TLS route:
```
oc create route edge langchain-chat --service=langchain-chat
```
