import os
from langchain_openai import AzureChatOpenAI

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
    raise ValueError(f"Unsupported provider: {provider}")