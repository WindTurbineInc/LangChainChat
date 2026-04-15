using System.Reflection;
using LangChain.Memory;
using LangChain.Providers;
using LangChain.Providers.OpenAI;
using LangChain.Serve;
using LangChain.Utilities.Classes.Repository;
using static LangChain.Chains.Chain;

namespace LangChainChat;

public static class LangChainConfigExtensions
{
    private const string NAME_GENERATOR_TEMPLATE =
        @"You will be given conversation between User and Assistant. Your task is to give a name to this conversation using maximum 3 words
Conversation:
{chat_history}
Conversation name: ";

    private const string CONVERSATION_MODEL_TEMPLATE =
        @"You are helpful assistant. Continue conversation with user. Keep your answers short.
{chat_history}
Assistant:";

    private static IChatModel CreateModelFromConfig(IConfigurationSection section)
    {
        var endpointUrl = section["Endpoint"] ?? "http://localhost:11434/v1";
        var modelId = section["ModelId"] ?? "mistral:latest";
        var apiKey = section["ApiKey"] ?? "EMPTY";
        var temperature = double.TryParse(section["Temperature"], out var temp) ? temp : 0.0;
        var stopSequences = section.GetSection("Stop").Get<string[]>() ?? new[] { "User:" };

        // The underlying tryAGI.OpenAI client expects just the domain (host:port),
        // not a full URL. Extract the domain from the configured endpoint.
        var uri = new Uri(endpointUrl);
        var domain = uri.Host + (uri.IsDefaultPort ? "" : $":{uri.Port}");

        var provider = new OpenAiProvider(apiKey: apiKey, customEndpoint: domain);

        // The OpenAI client defaults to HTTPS. For HTTP endpoints (e.g. local Ollama),
        // patch the internal URL format to use the correct scheme.
        if (uri.Scheme == "http")
        {
            var apiField = provider.GetType()
                .GetField("<Api>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var client = apiField.GetValue(provider)!;
            var settingsField = client.GetType()
                .GetField("<OpenAIClientSettings>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var settings = settingsField.GetValue(client)!;
            var urlFormatField = settings.GetType()
                .GetField("<BaseRequestUrlFormat>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var currentUrl = (string)urlFormatField.GetValue(settings)!;
            urlFormatField.SetValue(settings, currentUrl.Replace("https://", "http://"));
        }

        return new OpenAiChatModel(provider, id: modelId)
        {
            Settings = new OpenAiChatSettings
            {
                Temperature = temperature,
                StopSequences = stopSequences,
            }
        };
    }

    public static void ConfigureNameGenerator(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        var model = CreateModelFromConfig(configuration.GetSection("DefaultModel"));

        // generates name based on first messages of conversation
        serviceCollection.AddCustomNameGenerator(async messages =>
        {
            var template = NAME_GENERATOR_TEMPLATE;
            var conversationBufferMemory = await messages.ConvertToConversationBuffer();

            var chain = LoadMemory(conversationBufferMemory, "chat_history")
                        | Template(template)
                        | LLM(model);

            return await chain.Run("text");

        });
    }

    public static void ConfigureModels(this ServeOptions options, IConfiguration configuration)
    {
        // register the default model
        var defaultSection = configuration.GetSection("DefaultModel");
        var defaultName = defaultSection["Name"] ?? "Default Model";
        var defaultModel = CreateModelFromConfig(defaultSection);

        options.RegisterModel(defaultName, async (messages) =>
        {
            var template = CONVERSATION_MODEL_TEMPLATE;
            var conversationBufferMemory = await messages.ConvertToConversationBuffer();

            var chain = LoadMemory(conversationBufferMemory, "chat_history")
                        | Template(template)
                        | LLM(defaultModel);

            var response = await chain.Run("text");
            return new StoredMessage()
            {
                Author = MessageAuthor.AI,
                Content = response
            };
        });

        // register external models (LiteLLM, vLLM, or any OpenAI-compatible endpoint)
        var externalModels = configuration.GetSection("ExternalModels").GetChildren();
        foreach (var modelConfig in externalModels)
        {
            var name = modelConfig["Name"] ?? "External Model";
            var endpoint = modelConfig["Endpoint"] ?? "";
            var modelId = modelConfig["ModelId"] ?? "";

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(modelId))
                continue;

            var externalModel = CreateModelFromConfig(modelConfig);

            options.RegisterModel(name, async (messages) =>
            {
                var template = CONVERSATION_MODEL_TEMPLATE;
                var conversationBufferMemory = await messages.ConvertToConversationBuffer();

                var chain = LoadMemory(conversationBufferMemory, "chat_history")
                            | Template(template)
                            | LLM(externalModel);

                var response = await chain.Run("text");
                return new StoredMessage()
                {
                    Author = MessageAuthor.AI,
                    Content = response
                };
            });
        }
    }
}