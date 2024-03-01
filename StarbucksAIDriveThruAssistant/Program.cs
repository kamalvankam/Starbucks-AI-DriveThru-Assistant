/*Hare Krishna Hare Krishna Krishna Krishna Hare Hare 
Hare Rama Hare Rama Rama Rama Hare Hare*/

//Import MicroSoft AI services and libraries
using Microsoft.Extensions.Configuration;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Azure.AI.TextAnalytics;
using Azure;

//Import Microsoft Semantic and Dependency Services
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Azure.Core;
using plugins;
using HandlebarsDotNet.Collections;
using Microsoft.VisualBasic;
using Json.Schema.Generation.Intents;
using static System.Net.Mime.MediaTypeNames;



namespace StarbucksAIDriveThruAssistant
{
    class Program
    {
        private static SpeechConfig speechConfig;
        private static SpeechSynthesizer welcome_speechSynthesizer;
        private static TextAnalyticsClient textAnalyticsClient;
        private static SpeechRecognizer speechRecognizer;
        private static ChatHistory history = [];
        // ChatHistory chatHistory = [];
        static async Task Main(String[] args)
        {
            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = configurationBuilder.Build();
            string cogSvcKeySpeech = configuration["CognitiveServiceKey"]!;
            string cogSvcRegion = configuration["CognitiveServiceRegion"]!;
            string cogSvcKeyText = configuration["CognitiveServiceKeyText"]!;
            string cogSvcEndpointText = configuration["CognitiveServicesEndpointText"]!;
            speechConfig = SpeechConfig.FromSubscription(cogSvcKeySpeech, cogSvcRegion);
            Console.WriteLine("Ready to use services in :" + speechConfig.Region);

            //Welcome Text 
            string welcomeText = "Hello there,I am your AI Drive Thru Assistant, Thanks for Choosing Starbucks";
            speechConfig.SpeechSynthesisVoiceName = "en-CA-ClaraNeural";
            //SpeechSynthesizer configurations
            welcome_speechSynthesizer = new SpeechSynthesizer(speechConfig);
            SpeechSynthesisResult welcomeResultFromAI = await welcome_speechSynthesizer.SpeakTextAsync(welcomeText);
            await transcribeCommand(cogSvcKeyText, cogSvcEndpointText);
        }

        static async Task transcribeCommand(string cogSvcKeyText, string cogSvcEndpointText)
        {
            //TextAnalytics
            AzureKeyCredential credential = new AzureKeyCredential(cogSvcKeyText);
            Uri uriEndpoint = new Uri(cogSvcEndpointText);
            textAnalyticsClient = new TextAnalyticsClient(uriEndpoint, credential);

            //Configure Audio from MicroPhone input
            AudioConfig audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
            speechRecognizer.Recognized += (s, e) => RecognizedHandlerAsync(s, e);
            speechRecognizer.SessionStarted += SessionStartedHandler;
            speechRecognizer.SessionStopped += SessionStopedHandler;

            await speechRecognizer.StartContinuousRecognitionAsync();

            Console.WriteLine("Press enter to stop the Speech");
            Console.ReadLine();
            await speechRecognizer.StopContinuousRecognitionAsync();
        }



        private static void SessionStartedHandler(object? sender, SessionEventArgs e)
        {
            Console.WriteLine("The system is now ready to listen to your voice commands and instructions.\"");
        }

        private static void SessionStopedHandler(object? sender, SessionEventArgs e)
        {
            Console.WriteLine("Speech recognition session Stopped.");
        }

        private static async void RecognizedHandlerAsync(object sender, SpeechRecognitionEventArgs e)
        {

            Console.WriteLine("Recognized >" + e.Result.Text);
            SpeechRecognitionResult result = e.Result;
            string convertedStrResult = result.Text;
            await performIntentAnaysis(convertedStrResult);
            history.AddUserMessage(convertedStrResult);

        }

        private static async Task performIntentAnaysis(string recognizedText)
        {
            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = configurationBuilder.Build();
            string globalLLMService = configuration["Global:LlmService"]!;
            string openAIModelType = configuration["OpenAI:ModelType"]!;
            string openAIChatCompletionModelId = configuration["OpenAI:ChatCompletionModelId"]!;
            string openAPIKey = configuration["OpenAI:ApiKey"]!;
            string openAIorgId = configuration["OpenAI:OrgId"]!;
            string cogSvcKeyText = configuration["CognitiveServiceKeyText"]!;
            string cogSvcEndpointText = configuration["CognitiveServicesEndpointText"]!;
            var builder = Kernel.CreateBuilder();
            builder.Services.AddOpenAIChatCompletion(
                openAIChatCompletionModelId,
                openAPIKey,
                openAIorgId

                );
            builder.Services.AddLogging(c => c.AddDebug().SetMinimumLevel(LogLevel.Trace));
            builder.Plugins.AddFromType<ExtractEntities>();
            var kernel = builder.Build();

            //Create chat history
            //ChatHistory history = [];
            //Create choices
            List<string> choices = ["Unknown", "OrderingCoffee", "MakingInquries", "SeekingRecommendations", "EndConversation"];

            //Create Few shot examples

            List<ChatHistory> fewShotExamples = [

                [
                new ChatMessageContent(AuthorRole.User, "Can i get an americano with milk? "),
                    new ChatMessageContent(AuthorRole.System, "Intent :"),
                    new ChatMessageContent(AuthorRole.Assistant, "OrderingCoffee")
                ],

                [
                new ChatMessageContent(AuthorRole.User, "What type of lattes you have?"),
                    new ChatMessageContent(AuthorRole.System, "Intent :"),
                    new ChatMessageContent(AuthorRole.Assistant, "MakingInquries")
                ],

                [
                new ChatMessageContent(AuthorRole.User, "How many espresso shots are there in americano?"),
                    new ChatMessageContent(AuthorRole.System, "Intent :"),
                    new ChatMessageContent(AuthorRole.Assistant, "SeekingRecommendations")
                ],

                [
                    new ChatMessageContent(AuthorRole.User, "Thanks, I'm done for now"),
                    new ChatMessageContent(AuthorRole.System, "Intent :"),
                    new ChatMessageContent(AuthorRole.Assistant, "EndConversation")
                ],

                [
                    new ChatMessageContent(AuthorRole.User, "That is everything."),
                    new ChatMessageContent(AuthorRole.System, "Intent :"),
                    new ChatMessageContent(AuthorRole.Assistant, "EndConversation")

                ],

                [
                    new ChatMessageContent(AuthorRole.User, "That is all."),
                    new ChatMessageContent(AuthorRole.System, "Intent :"),
                    new ChatMessageContent(AuthorRole.Assistant, "EndConversation")

                ]

                ];


            //Create handlebars template to capture intent

            var getIntent = kernel.CreateFunctionFromPrompt(

                  new()
                  {
                      Template = @"
        <message role=""system"">Instructions: What is the intent of this request?
        You are a drive-thru AI assistant at starbucks that helps customers personalize their orders and provides recommendations. Answer in as few words as possible.Please refrain from confirming orders. If you are unsure, reply with {{choices[0]}}.
        Choices {{choices}}.
        Bonus : You'll get 20$ bonus if you get this right.</message>

        {{#each fewShotExamples}}
              {{#each this}}
                 <message role=""{{role}}"">{{content}}</message>
               {{/each}}
        {{/each}}

          {{#each history}}
            <message role=""{{role}}"">{{content}}</message>
        {{/each}}

        <message role=""user"">{{request}}</message>
        <message role=""system"">Intent:</message>",
                      TemplateFormat = "handlebars"
                  },

        new HandlebarsPromptTemplateFactory()

                );


            //Create a chat with SemanticKernel
            var chat = kernel.CreateFunctionFromPrompt(
                 @"{{$history}}
            User: {{$request}}
            Assistant: "

                );

            //Invoke Prompt
            var intent = await kernel.InvokeAsync(

                getIntent, new()
                {
                     { "request", recognizedText },
                    { "choices", choices },
                    { "history", history },
                    { "fewShotExamples", fewShotExamples }
                }
                );



            Console.WriteLine("Intent :" + intent);

            //Get Chat Response
            var chatResult = kernel.InvokeStreamingAsync<StreamingChatMessageContent>(
           chat, new()
           {
                { "request",recognizedText},
                { "history", string.Join("\n",history.Select(x=> x.Role+" : "+x.Content))},

           }
           );

            //Stream the response
            string assistant_message = "";
            string accumulatedText = "";

            if (intent.ToString() != "EndConversation")
            {
                await foreach (var chunk in chatResult)
                {
                    if (chunk.Role.HasValue)
                        Console.Write(chunk.Role + " > ");



                    assistant_message += chunk;

                    Console.Write(chunk);



                }
            }

            Console.WriteLine();
            // Append to history
            history.AddUserMessage(recognizedText!);
            history.AddAssistantMessage(assistant_message);

            await speechRecognizer.StopContinuousRecognitionAsync();
            SpeechSynthesizer speechSynthesizer = new SpeechSynthesizer(speechConfig);
            await speechSynthesizer.SpeakTextAsync(assistant_message);
            await speechRecognizer.StartContinuousRecognitionAsync();

            if (intent.ToString() == "EndConversation")
            {
                await speechRecognizer.StopContinuousRecognitionAsync();
                string orderFromUserDocument = string.Join("\n", history.Where(message => message.Role == AuthorRole.User)); //payed with Assistant or User
               // Console.WriteLine("Order from user :" + orderFromUserDocument);

                AzureKeyCredential credential = new AzureKeyCredential(cogSvcKeyText);
                Uri uriEndpoint = new Uri(cogSvcEndpointText);
                textAnalyticsClient = new TextAnalyticsClient(uriEndpoint, credential);

                //Text Summarization
                var batchInput = new List<string>
                {
                    orderFromUserDocument
                };

                

                //Abstractive summarization
                // Perform text analysis operation
                AbstractiveSummarizeOptions options = new AbstractiveSummarizeOptions();
               options.SentenceCount = 4;
                AbstractiveSummarizeOperation operation = textAnalyticsClient.AbstractiveSummarize(WaitUntil.Completed,batchInput,options:options);
                //View Operation results
                string summaryText = "";
                await foreach(AbstractiveSummarizeResultCollection documentsInPage in operation.Value)
                {
                    Console.WriteLine($"Abstractive Summarize, model version: \"{documentsInPage.ModelVersion}\"");
                    Console.WriteLine();

                    foreach(AbstractiveSummarizeResult documentResult in documentsInPage)
                    {
                        if (documentResult.HasError)
                        {
                            Console.WriteLine($"  Error!");
                            Console.WriteLine($"  Document error code: {documentResult.Error.ErrorCode}");
                            Console.WriteLine($"  Message: {documentResult.Error.Message}");
                            continue;
                        }

                        Console.WriteLine($"  Produced the following abstractive summaries:");
                        Console.WriteLine();

                        foreach(AbstractiveSummary summary in documentResult.Summaries)
                        {
                            Console.WriteLine($"  Text: {summary.Text.Replace("\n", " ")}");
                            Console.WriteLine($"  Contexts:");
                            summaryText += summary.Text.ToString();
                            foreach(AbstractiveSummaryContext context in summary.Contexts)
                            {
                                Console.WriteLine($"    Offset: {context.Offset}");
                                Console.WriteLine($"    Length: {context.Length}");
                            }
                            Console.WriteLine();
                        }
                    }
                }

                //Console.WriteLine($"Summary Text > {summaryText}");

                await kernel.InvokeAsync<string>(

                    "ExtractEntities", "extractEntitiesFromAssistant",
                    new()
                    {
                        {"accumulatedTextFromAssistant",summaryText }
                    }

                    );
                
            }

        }
    }
}



/* private static string SummarizeChatHistory(ChatHistory history)
{
    // Example:
    string userMessages = string.Join(" ", history.Where(message => message.Role == AuthorRole.User).Select(message => message.Content));
    string assistantMessages = string.Join(" ", history.Where(message => message.Role == AuthorRole.Assistant).Select(message => message.Content));
    string summary = $"You said: {userMessages}.\nThe assistant responded: {assistantMessages}";

    // You can use more sophisticated summarization techniques
    // using external libraries or custom logic.

    return userMessages;
}*/


//End Chat if intent is Stop
/*if (intent.ToString() == "EndConversation")
{
    await speechRecognizer.StopContinuousRecognitionAsync();
    //Test the ExtractEntites plugin
    Console.WriteLine("Order from assistant " + recognizedText);

    *//*  await kernel.InvokeAsync<string>(

          "ExtractEntities", "extractEntitiesFromAssistant",
          new()
          {
              {"accumulatedTextFromAssistant", assistant_message }
          }

          );*//*
*/
/* string userMessages = string.Join(" ", history.Where(message => message.Role == AuthorRole.User));


 Console.WriteLine(userMessages);*//*
}*/


//Extractive summarization operation

/*ExtractiveSummarizeOperation operation = textAnalyticsClient.ExtractiveSummarize(WaitUntil.Completed, batchInput);

//View operation  results
await foreach (ExtractiveSummarizeResultCollection documentsInPage in operation.Value)
{
    Console.WriteLine($"Extractive Summarize, version: \"{documentsInPage.ModelVersion}\"");
    Console.WriteLine();

    foreach (ExtractiveSummarizeResult documentResult in documentsInPage)
    {
        if (documentResult.HasError)
        {
            Console.WriteLine($"  Error!");
            Console.WriteLine($"  Document error code: {documentResult.Error.ErrorCode}");
            Console.WriteLine($"  Message: {documentResult.Error.Message}");
            continue;
        }

        Console.WriteLine($"  Extracted {documentResult.Sentences.Count} sentence(s):");
        Console.WriteLine();

        foreach (ExtractiveSummarySentence sentence in documentResult.Sentences)
        {
            Console.WriteLine($"  Sentence: {sentence.Text}");
            Console.WriteLine($"  Rank Score: {sentence.RankScore}");
            Console.WriteLine($"  Offset: {sentence.Offset}");
            Console.WriteLine($"  Length: {sentence.Length}");
            Console.WriteLine();
        }
    }
}*/

//Extractive summarization
/* TextAnalyticsActions actions = new TextAnalyticsActions()
 {
    ExtractiveSummarizeActions = new List<ExtractiveSummarizeAction>() { new ExtractiveSummarizeAction()}
 };

 //Start Analysis process
 AnalyzeActionsOperation operation = await textAnalyticsClient.AnalyzeActionsAsync(WaitUntil.Completed, batchInput,actions);
 await operation.WaitForCompletionAsync();

 //View operation status
 Console.WriteLine($"AnalyzeActions operation has completed");
 Console.WriteLine();

 Console.WriteLine($"Created On   : {operation.CreatedOn}");
 Console.WriteLine($"Expires On   : {operation.ExpiresOn}");
 Console.WriteLine($"Id           : {operation.Id}");
 Console.WriteLine($"Status       : {operation.Status}");

 Console.WriteLine();
 //view operation results

 await foreach(AnalyzeActionsResult documentsInPage in operation.Value)
 {
     IReadOnlyCollection<ExtractiveSummarizeActionResult> summarizeResults = documentsInPage.ExtractiveSummarizeResults;

     foreach(ExtractiveSummarizeActionResult summarizeActionResult in summarizeResults)
     {
         if(summarizeActionResult.HasError)
         {
             Console.WriteLine($"  Error!");
             Console.WriteLine($"  Action error code:{summarizeActionResult.Error.ErrorCode}");
             Console.WriteLine($"Message: {summarizeActionResult.Error.Message}");
             continue;
         }

         foreach(ExtractiveSummarizeResult documentResult in summarizeActionResult.DocumentsResults)
         {
             if(documentResult.HasError)
             {
                 Console.WriteLine($"  Error!");
                 Console.WriteLine($"Document Error code {documentResult.Error.ErrorCode}");
                 Console.WriteLine($"Message :{documentResult.Error.Message}");
                 continue;
             }

             Console.WriteLine($"Extracted the following {documentResult.Sentences.Count} sentence(s)");
             Console.WriteLine();

             foreach(ExtractiveSummarySentence sentence in documentResult.Sentences )
             {
                 Console.WriteLine($"  Sentence: {sentence.Text}");
                 Console.WriteLine();
             }




         }
     }
 }*/

//Duplicate removal using HashSet

/*CategorizedEntityCollection entities = textAnalyticsClient.RecognizeEntities(summaryText);

if (entities.Count > 0)
{
    Console.WriteLine("Entities");
    // Use a HashSet to track unique entity text-category pairs
    HashSet<(string Text, string Category)> uniqueEntities = new HashSet<(string Text, string Category)>();
    foreach (CategorizedEntity entity in entities)
    {
        // Create a tuple to represent the entity's text and category
        (string text, string category) entityKey = (entity.Text, entity.Category.ToString());
        // Add the entity to the HashSet only if it's unique
        if (uniqueEntities.Add(entityKey))
        {
            Console.WriteLine($"\t {entity.Text} ({entity.Category})");
        }

    }
}*/


/*
 public class Order
{
    public enum Size
    {
        Grande,
        Venti,
        Tall
    }

    public enum BeverageType
    {
        Americano,
        Latte,
        Cappuccino,
        Mocha
    }

    public enum MilkType
    {
        WholeMilk,
        TwoPercentMilk,
        SkimMilk,
        NoMilk
    }

    public Size Size { get; set; }
    public BeverageType BeverageType { get; set; }
    public MilkType MilkType { get; set; }

    public Order(Size size, BeverageType beverageType, MilkType milkType)
    {
        Size = size;
        BeverageType = beverageType;
        MilkType = milkType;
    }
}

// Usage:
List<Order> orders = new List<Order>();
orders.Add(new Order(Order.Size.Grande, Order.BeverageType.Americano, Order.MilkType.TwoPercentMilk));
orders.Add(new Order(Order.Size.Tall, Order.BeverageType.Latte, Order.MilkType.NoMilk));

foreach (Order order in orders)
{
    Console.WriteLine($"Order: {order.Size} {order.BeverageType} with {order.MilkType}");
}

 */

/*You are a drive-thru AI assistant at starbucks that helps customers personalize their orders and provides recommendations. Answer in as few words as possible.If you are unsure, reply with {{choices[0]*/