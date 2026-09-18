import os
from dotenv import load_dotenv
from langchain_openai import AzureChatOpenAI
from langchain_google_genai import ChatGoogleGenerativeAI

load_dotenv()

def get_llm():
    provider = os.getenv("LLM_PROVIDER", "azure_openai").lower()
    
    if provider == "azure_openai":
        return AzureChatOpenAI(
            azure_deployment=os.getenv("AZURE_OPENAI_DEPLOYMENT_NAME"),
            openai_api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
            azure_endpoint=os.getenv("AZURE_OPENAI_ENDPOINT"),
            api_key=os.getenv("AZURE_OPENAI_API_KEY"),
            temperature=0
        )
    elif provider == "gemini":
        return ChatGoogleGenerativeAI(
            model="gemini-1.5-flash",
            google_api_key=os.getenv("GEMINI_API_KEY"),
            temperature=0
        )
    raise ValueError(f"Unsupported provider: {provider}")