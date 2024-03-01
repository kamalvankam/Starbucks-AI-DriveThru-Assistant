using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;






//import services
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using plugins;


namespace plugins
{
    public class ExtractEntities
    {
        //List parameters
        private static List<string> listOfOrderSize;
        private static List<string> listOfOrdersBeverages;
        private static List<string> listOfMilk ;
        private static List<CustomerOrder> customerOrders ;
        private static string milkType="No milk specified";
        private static string beverageFromEntity;
        private static string beverageSize;
        private static SpeechSynthesizer orderConfirmSynthesizer;
        private static SpeechConfig speechConfig;
        private static SpeechRecognizer speechRecognizer = null;

        private static TextAnalyticsClient textAnalyticsClient;
        private static List<string> orderDetails = new List<string>();
        [KernelFunction, Description("Extract Entities from the text")]
        public static async Task extractEntitiesFromAssistant([Description("Extract custom entites from ")] string accumulatedTextFromAssistant)
        {
            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = configurationBuilder.Build();
            string cogSvcKeyText = configuration["CognitiveServiceKeyText"]!;
            string cogSvcEndpointText = configuration["CognitiveServicesEndpointText"]!;
            string cogSvcKeySpeech = configuration["CognitiveServiceKey"]!;
            string cogSvcRegion = configuration["CognitiveServiceRegion"]!;
            AzureKeyCredential credential = new AzureKeyCredential(cogSvcKeyText);
            Uri endpoint = new Uri(cogSvcEndpointText);
            textAnalyticsClient = new TextAnalyticsClient(endpoint, credential);

            //speech config
            speechConfig = SpeechConfig.FromSubscription(cogSvcKeySpeech, cogSvcRegion);
            string projectName = "CustomCoffeeModelV2";
            string deploymentName = "ProductionCustomModelV2";
            listOfOrderSize = new List<string>();
            listOfOrdersBeverages = new List<string>();
            listOfMilk = new List<string>();
            customerOrders = new List<CustomerOrder>();

            List<TextDocumentInput> batchedDocuments = new List<TextDocumentInput>
            {
                new TextDocumentInput("one", accumulatedTextFromAssistant)
                {
                    Language = "en",
                }
            };

            RecognizeCustomEntitiesOperation operation = textAnalyticsClient.RecognizeCustomEntities(WaitUntil.Completed, batchedDocuments, projectName, deploymentName);
            await foreach (RecognizeCustomEntitiesResultCollection documentPage in operation.Value)
            {
                foreach (RecognizeEntitiesResult documentResult in documentPage)
                {
                    Console.WriteLine($"Result for document with Id = \"{documentResult.Id}\":");

                    if (documentResult.HasError)
                    {
                        Console.WriteLine($"  Error!");
                        Console.WriteLine($"  Document error code: {documentResult.Error.ErrorCode}");
                        Console.WriteLine($"  Message: {documentResult.Error.Message}");
                        Console.WriteLine();
                        continue;
                    }

                    Console.WriteLine($"  Recognized {documentResult.Entities.Count} entities:");
                    // List<string> currentOrderDetails = new List<string>();
                    CustomerOrder order = null;
                    string previousMilkType = null;
                    foreach (CategorizedEntity entity in documentResult.Entities)
                    {
                        /*Console.WriteLine($"  Entity: {entity.Text}");
                        Console.WriteLine($"  Category: {entity.Category}");
                        Console.WriteLine($"  Offset: {entity.Offset}");
                        Console.WriteLine($"  Length: {entity.Length}");
                        Console.WriteLine($"  ConfidenceScore: {entity.ConfidenceScore}");
                        Console.WriteLine($"  SubCategory: {entity.SubCategory}");
                        Console.WriteLine();*/

                        
                        if (entity.ConfidenceScore >0.95 && entity.ConfidenceScore<= 1.0)
                        {


                            if (order == null)
                            {
                                order = new CustomerOrder();
                            }
                            if (entity.Category.Equals("Size"))
                            {
                                order.setSize(entity.Text);
                            }
                            else if ((entity.Category.Equals("Mochas") || entity.Category.Equals("Americanos") || entity.Category.Equals("Brewed Coffee") || entity.Category.Equals("Cappuccinos")))
                            {
                                string beverage = entity.Text;
                                    order.setBeverage(entity.Text);

                                int nextIndex = documentResult.Entities.IndexOf(entity) + 1;
                                if(nextIndex<documentResult.Entities.Count)
                                {
                                    CategorizedEntity nextEntity= documentResult.Entities[nextIndex];
                                    if(nextEntity.Category.Equals("Dairy") || nextEntity.Category.Equals("Non Dairy"))
                                    {
                                        order.setMilk(nextEntity.Text);
                                    }
                                    else
                                    {
                                        order.setMilk("");
                                    }
                                }
                                
                            }

                            /*if ((entity.Category.Equals("Dairy") || entity.Category.Equals("Non Dairy")) )
                            {
                                order.setMilk(entity.Text);

                            }
*/



                            if (order.getSize() != null && order.getBeverage() != null)
                            {
                                //order.setMilk(order.getMilktype() ?? "No milk specified");
                                customerOrders.Add(order);
                               //Reset the milk for next order
                                order = null;  // Reset for the next order
                                
                            }
                            //&& order.getMilktype() != null 

                        }

                       


                    }
                    


                }


            }


            await ConfrimOrder();
        }



        public static async Task ConfrimOrder()
        {
            speechConfig.SpeechSynthesisVoiceName = "en-CA-ClaraNeural";
            SpeechSynthesizer speechSynthesizer = new SpeechSynthesizer(speechConfig);
            await speechSynthesizer.SpeakTextAsync("Your order includes");
            foreach (CustomerOrder order in customerOrders)
            {
                await speechSynthesizer.SpeakTextAsync(order.getSize()+order.getBeverage());
                if(order.getMilktype()!="")
                {
                    await speechSynthesizer.SpeakTextAsync("with"+order.getMilktype());
                }
                Console.WriteLine($"\t{order.getSize()} \t{order.getBeverage()}");
                Console.WriteLine($"\t{order.getMilktype()}");
            }
            await speechSynthesizer.SpeakTextAsync("Please confirm your order. Is everything correct?");
            //Process speech input
            AudioConfig audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            speechRecognizer = new SpeechRecognizer(speechConfig,audioConfig);
            SpeechRecognitionResult speechRes = await speechRecognizer.RecognizeOnceAsync();
            string commamd = speechRes.Text;
            Console.WriteLine("Confirmation text :" + commamd);
            if (commamd.StartsWith("Yes"))
            {
             
                await speechSynthesizer.SpeakTextAsync ("Thank you for your order. It has been confirmed and is currently being processed.");
                await printOrder();
            }
            else
            {
                await speechSynthesizer.SpeakTextAsync("Startover again");
            }
        }


        public static async Task printOrder()
        {
            var timeNow = DateTime.Now;




            Console.WriteLine("Customer Orders:");
            int totalOrders = customerOrders.Count;
            int index = 0;
           
            foreach (CustomerOrder order in customerOrders)
            {
                Console.WriteLine("----------------------print-------------");
                index++;
                Console.WriteLine($"\tItem:{index} of {customerOrders.Count}");
                Console.WriteLine($"\tItems in Orders: {totalOrders}");
                Console.WriteLine($"\t{order.getSize()} \t{order.getBeverage()}");
                Console.WriteLine($"\t{order.getMilktype()}");
                Console.WriteLine($"\t{timeNow.ToString("dd-MMM-yyyy")}\t{timeNow.ToString("h:mm tt")}");
                Console.WriteLine($"\t>Drive Thru<");
            }





        }


    }
 }



/* for (int i = 0; i < customerOrders.Count; i++)
             {
                 Console.WriteLine("----------------------print-------------");
                 // Console.WriteLine($"\tItem: {i + 1} of {customerOrders[i].getSize}");
                  Console.WriteLine($"\tItems in order: {listOfOrderSize.Count}");
                // Console.WriteLine($"\t{customerOrders[i].getSize()}\t{customerOrders[i].getBeverage()}");
                 Console.WriteLine($"\t{listOfOrderSize[i]}\t{listOfOrdersBeverages[i]}");

                 if (listOfMilk[i] == null)
                 {
                     Console.WriteLine("");
                 }
                 else
                 {
                     Console.WriteLine($"\t{listOfMilk[i]}");
                 }


                 //Console.WriteLine($"\t{customerOrders[i].getMilktype()}");

                 //  Console.WriteLine($"\t{listProduct[i]}");
                 Console.WriteLine($"\t{timeNow.ToString("dd-MMM-yyyy")}\t{timeNow.ToString("h:mm tt")}");
                 Console.WriteLine($"\t>Drive Thru<");

             }*/


/*if (listOfMilk != null && i >= 0 && i < listOfMilk.Count)
               {

               }*/
/*else
{
    Console.WriteLine("\tNo milk specified");

}*//*
//  Console.WriteLine($"\t{listProduct[i]}");


Console.WriteLine($"\t{timeNow.ToString("dd-MMM-yyyy")}\t{timeNow.ToString("h:mm tt")}");
Console.WriteLine($"\t>Drive Thru<");



/* foreach(CustomerOrder order in customerOrders)
{
Console.WriteLine($"{order.getSize()} {order.getBeverage()} {order.getMilktype()}");
}
*/


/*
 
 List<Order> orders = new List<Order>();
Order currentOrder = null; // Initialize a variable to hold the current order

// ... your code for processing entities ...

if (entity.ConfidenceScore == 1.0)
{
    if (entity.Category.Equals("Size"))
    {
        beverageSize = entity.Text;
        // Create a new order if it doesn't exist yet
        if (currentOrder == null)
        {
            currentOrder = new Order();
        }
        currentOrder.setSize(beverageSize);
    }
    else if (entity.Category.Equals("Mochas") || 
             entity.Category.Equals("Americanos") || 
             entity.Category.Equals("Brewed Coffee") || 
             entity.Category.Equals("Cappuccinos"))
    {
        beverageFromEntity = entity.Text;
        if (currentOrder == null)
        {
            currentOrder = new Order();
        }
        currentOrder.setBeverage(beverageFromEntity);
    }
    else if (entity.Category.Equals("Dairy") || entity.Category.Equals("Non Dairy"))
    {
        milkType = new string(entity.Text);
        if (currentOrder == null)
        {
            currentOrder = new Order();
        }
        currentOrder.setMilk(milkType);

        // Add the completed order to the list
        orders.Add(currentOrder);
        currentOrder = null; // Reset for the next order
    }
}

 */


/* for(int i = 0; i<listOfOrderSize.Count; i++)
           {
               Console.WriteLine("Size list:" + listOfOrderSize[i]);
           }

           for(int i=0; i< listOfOrdersBeverages.Count; i++)
           {
               Console.WriteLine("Beverages :"+listOfOrdersBeverages[i]);

           }
           for(int i=0;i < listOfMilk.Count; i++)
           {
               Console.WriteLine("Milk :" + listOfMilk[i]);
           }*/

/* for (int i = 0; i < listOfOrderSize.Count; i++)
           {
               Console.WriteLine("----------------------print-------------");
               // Console.WriteLine($"\tItem: {i + 1} of {listOfOrderSize.Count + listProduct.Count}");

               Console.WriteLine($"\t{listOfOrderSize[i]}\t{listOfOrdersBeverages[i]}");
                 Console.WriteLine($"\t{listOfMilk[i]}");


           }*/


/*
 * 
 * for (int i = 0; i < customerOrders.Count; i++)
{
    CustomerOrder order = customerOrders[i];
    Console.WriteLine($"{order.getSize()} {order.getBeverage()} {order.getMilktype()}");
}

 */


/* Order check 
 * foreach (CustomerOrder order in customerOrders)
{
  Console.WriteLine("---------------------- Order Review -------------");
  index++;
  Console.WriteLine($"\tItem: {index} of {customerOrders.Count}");

  // Order details
  Console.WriteLine($"\tSize: {order.getSize()}");
  Console.WriteLine($"\tBeverage: {order.getBeverage()}");
  Console.WriteLine($"\tMilk: {order.getMilktype()}");

  // Confirmation prompt
  string confirmation = Console.ReadLine("Is this order correct? (yes/no): ");

  // Process confirmation
  if (confirmation.ToLower() == "yes")
  {
    Console.WriteLine("Order confirmed!");
    // Proceed with order processing
  }
  else if (confirmation.ToLower() == "no")
  {
    Console.WriteLine("Please let us know what you would like to change.");
    // Allow customer to modify the order (implement logic for modification)
  }
  else
  {
    Console.WriteLine("Please respond with 'yes' or 'no'.");
  }
}
*/

/*if (beverageSize != null && beverageFromEntity != null)
                       {
    //customerOrders.Add(new CustomerOrder(beverageSize, beverageFromEntity, milkType));
    CustomerOrder customerOrder = new CustomerOrder(beverageSize, beverageFromEntity, milkType);
    customerOrder.AddOrder(customerOrder);
    customerOrders.Add(customerOrder);
}
*/