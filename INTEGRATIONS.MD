----------

# 🔌 Nexus Intake: Data Flow & Integration Guide

This document outlines the exact lifecycle of a document scan within the Nexus Intake pipeline, details the data payload it generates, and provides architectural patterns for binding this data to external applications, databases, and autonomous frameworks.

----------

## 🌊 1. The Data Flow Architecture

The pipeline operates in a synchronous request-response cycle, utilizing a mix of C# webhook handling and Python-based AI inference.

1.  **Ingestion (Telegram):** The user sends a photo of a TRNC Kimlik or Insurance Policy to the bot.
    
2.  **Reception (.NET API):** The Cloud Run instance intercepts the `POST` webhook. `Newtonsoft.Json` deserializes the payload, extracting the `file_id`.
    
3.  **Storage (GCS):** The C# application downloads the image from Telegram's servers and pushes it to a Google Cloud Storage bucket (`nexus-intake-raw`), generating a secure `gs://` URI.
    
4.  **Inference (Python CF):** The C# app fires an HTTP request to the Python Cloud Function, passing the `gs://` URI.
    
5.  **Extraction (Vertex AI):** The Python function passes the image to `gemini-1.5-flash-001` with strict instructions to return a structured JSON string adhering to the predefined schema.
    
6.  **Return & Format:** The C# application receives the JSON, deserializes it into the `ExtractionResult` class, formats it via `CyberTerminalFormatter`, and pushes the markdown response back to the user via Telegram.
    

 **Injection Point:** The optimal place to bind external apps is immediately after Step 6, right before (or asynchronously alongside) sending the Telegram confirmation.

----------

## 📦 2. The Standardized Payload

Regardless of whether the user uploaded an ID card or a policy document, the Python extraction engine will always return a predictable JSON structure.

Here is what the C# application holds in memory after a successful scan:

JSON

```
{
  "documentType": "Kimlik",
  "confidenceScore": 0.98,
  "extractedData": {
    "fullName": "MERT YILMAZ",
    "trncIdNumber": "1234567890",
    "dateOfBirth": "1990-05-15",
    "policyNumber": null,
    "vehiclePlate": null,
    "expirationDate": null
  },
  "rawText": "...raw OCR data...",
  "validationErrors": []
}

```

----------

## 🔗 3. Binding to Other Applications

Once the data is structured, you can route it anywhere. Here are the three primary integration patterns for your C# backend.

### Pattern A: Relational Databases (Entity Framework / SQL)

To permanently store these leads, you can map the `ExtractionResult` directly to a database entity. In your `CustomerLeadService.cs`, instead of the temporary `HashSet`, inject a DbContext:

C#

```
public async Task SaveLeadAsync(ExtractionResult result, CancellationToken ct)
{
    // 1. Map the JSON result to your internal database model
    var newLead = new LeadEntity
    {
        FullName = result.ExtractedData.FullName,
        NationalId = result.ExtractedData.TrncIdNumber,
        PolicyNumber = result.ExtractedData.PolicyNumber,
        Source = "TelegramBot",
        ScannedAt = DateTime.UtcNow
    };

    // 2. Save to database (e.g., PostgreSQL or SQL Server)
    _dbContext.Leads.Add(newLead);
    await _dbContext.SaveChangesAsync(ct);
}

```

### Pattern B: The Agentic Automation Layer

If you are piping these leads into an autonomous framework, the structured JSON is ready for immediate consumption. You can configure your C# API to fire a lightweight webhook to a local or cloud-based AI agent (like OpenClaw or an orchestration script).

The agent can listen for this payload and automatically execute secondary tasks:

-   Cross-referencing the `TrncIdNumber` against a legacy database.
    
-   Drafting a personalized welcome email if the record is new.
    
-   Calculating risk metrics based on the `vehiclePlate` and policy dates.
    

C#

```
// Example: Firing the payload to an autonomous agent listener
var agentPayload = new StringContent(JsonConvert.SerializeObject(result), Encoding.UTF8, "application/json");
await _httpClient.PostAsync("http://your-agent-framework:8000/intake", agentPayload);

```

### Pattern C: Workspace & CRM Integration (Notion API)

For visual tracking, lead management, or integrating with a broader workspace setup, routing this payload directly into a Notion database is highly effective.

You can use the official `Notion.Net` SDK in your C# app to create a new page in a database the second a scan completes:

C#

```
// Example: Creating a Notion Database Entry
var newPage = new PagesCreateParameters
{
    Parent = new DatabaseParentInput { DatabaseId = "YOUR_NOTION_DB_ID" },
    Properties = new Dictionary<string, PropertyValue>
    {
        { "Name", new TitlePropertyValue { Title = new List<RichTextBase> { new RichTextText { Text = new Text { Content = result.ExtractedData.FullName } } } } },
        { "ID Number", new RichTextPropertyValue { RichText = new List<RichTextBase> { new RichTextText { Text = new Text { Content = result.ExtractedData.TrncIdNumber } } } } },
        { "Status", new SelectPropertyValue { Select = new SelectOption { Name = "New Lead" } } }
    }
};

await _notionClient.Pages.CreateAsync(newPage);

```

### Pattern D: Event Streaming (Pub/Sub)

If you plan to scale this so that multiple different apps (billing, marketing, compliance) all react to a single scan, you should publish the JSON to Google Cloud Pub/Sub.

1.  The C# app publishes the `ExtractionResult` JSON to a Pub/Sub topic (e.g., `nexus-new-scans`).
    
2.  Your other applications (Python scripts, Node.js backends, etc.) subscribe to that topic and process the data at their own pace without slowing down the Telegram bot.