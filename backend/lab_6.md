SLIIT  |  FACULTY OF COMPUTING  |  DEPARTMENT OF SOFTWARE ENGINEERING
SE3090 – Software Engineering Frameworks
Lab Practical 06
Agentic Software Development – Part 2: A Grounded, Stateful Agent
Lab Practical Number Lab 06  —  “A Grounded, Stateful Agent”
Related Lecture Lecture 06 – Agentic Software Development 2
Duration 2 hours
Cost to student Rs. 0  ($0)  —  Gemini API free tier.  Budget: approx. 45 model requests.
Learning Outcomes LO1 • LO2 • LO3 • LO4
Lab pack SE3090_Lab06_Agentic_AI_Part_2.zip  —  everything you need is in this folder
Prior knowledge Week 5 is assumed for ideas (the agent loop, tools, create_agent), not for 
plumbing: this folder installs and configures itself from scratch.
Deliverables lab6_evidence.txt with the six checkpoints, plus lab.ipynb with Parts 1, 3 and 5 
completed.
How to Use This Sheet
READ THIS FIRST
This is the written walkthrough of the lab: every concept, every piece of code, and an explanation of 
what each piece does and why. lab.ipynb is where you type; this sheet is what you read. 
README.md next door is the short logistics card.
Code blocks marked Your turn are the ones you write. Each is followed by a reference implementation — 
read it after an honest attempt.
Week 5 is assumed for ideas (the agent loop, tools, create_agent), not for plumbing: this folder 
installs and configures itself from scratch.
Contents
§ Section
1 What you are building
2 Setup and environment
3 Part 1 — Ingest and hybrid search
4 Part 2 — Agentic RAG with create_agent
5 Part 3 — The explicit graph: grade and self-correct
6 Part 4 — Memory via checkpointing
7 Part 5 — Human-in-the-loop refunds
8 Part 6 — The workflow behind FastAPI
9 Wrap-up, deliverables, stretch goals
10 Troubleshooting
11 Glossary
SLIIT  |  Faculty of Computing  |  Department of Software Engineering Page 1 of 24
SE3090 – Software Engineering Frameworks
Lab Practical 06
SECTION 1
What You Are Building
A support agent for the fictional retailer Contoso Outdoor Gear with four properties that a demo chatbot 
does not have:
             +-------------------- the graph you build --------------------+
             |                                                             |
START -> router --"refund"--> human_gate [||] -> process_refund -> END     |
          |                     (a human decides here)                     |
       "question"                                                          |
          v                                                                |
      retrieve -> grade --relevant--> answer -> END                        |
          ^           |                                                    |
          +- rewrite <+ irrelevant  (max 2 retries, then answer honestly)  |
             +-------------------------------------------------------------+
Property
Mechanism you implement
Grounded
Answers come from an index you build, with [source] citations
Self-correcting
A grader judges the retrieval; a bad one triggers a query rewrite and retry
Stateful
A checkpointer stores state per conversation thread
Supervised
A refund pauses the graph until a human approves or denies
…and then you serve the whole thing, human gate included, over HTTP.
Learning objectives
1. Explain the offline RAG pipeline (load → chunk → embed → store) and what each stage decides.
2. Implement Reciprocal Rank Fusion and say why hybrid search beats either half alone.
3. Contrast classic RAG with agentic RAG, and name the cost of the difference.
4. Express control flow as an explicit graph: state, reducers, nodes, conditional edges.
5. Explain what a checkpointer stores, and why human-in-the-loop is impossible without one.
6. Drive a paused workflow to completion across two separate HTTP requests.
What is in this folder
File
Purpose
labsheet.md
This document.
README.md
Setup commands, run sheet, checkpoints, demonstrator notes.
lab.ipynb  ·  
solutions.ipynb
The lab, and reference implementations.
demos.ipynb
The lecture demo: cosine ranking and the negation blind spot.
api/main.py
The finished graph as a FastAPI service.
data/
The corpus: returns policy, warranty guide, product FAQ.
chroma_db/
Created by the ingest step — your vector database, as a folder.
Page 2 of 24
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
SE3090 – Software Engineering Frameworks Lab Practical 06
SECTION 2
Setup and Environment
2.1  Commands (run from week-6/lab/)
python3 -m venv .venv
source .venv/bin/activate                 # Windows: .venv\Scripts\activate
pip install -U pip
pip install -r requirements.txt           # ~2-3 min; chromadb is the big one
python -m ipykernel install --user --name agentic-w6 \
       --display-name "Agentic AI (week 6)"
cp .env.example .env        # then paste your Gemini API key
                            # into GOOGLE_API_KEY
jupyter lab lab.ipynb
2.2  What each dependency is for
Package Why it is here
langchain-google-genai Gemini chat and embeddings through the same free Google AI Studio key.
langgraph Explicit control flow: state, nodes, conditional edges, checkpointing, interrupts.
langchain-chroma, 
chromadb The local vector store — a folder on your disk, not a cloud service.
langchain-community, 
rank-bm25 BM25Retriever: the keyword half of hybrid search.
langgraph-checkpoint
sqlite Durable checkpointing for stretch goal 4.
fastapi, uvicorn, 
requests Part 6.
2.3  Two models this week
Purpose Default Notes
Chat / reasoning gemini-2.5-flash Router, grader, rewriter, answerer
Embeddings gemini-embedding-001 Used once at ingest, and once per search
Both are free on the Gemini API (Google AI Studio) and already in .env.example, along with 
VECTOR_COLLECTION=contoso-handbook. One key covers both — you created it in Week 5; if you 
need a new one, it is two clicks at https://aistudio.google.com/apikey.
2.4  Kernels: the thing that breaks first
The single most common failure in this lab is not a broken install. It is this:
your terminal                          JupyterLab-------------                          ---------
source .venv/bin/activate      the notebook picks a KERNEL
pip install -r requirements.txt   (a separate Python process)
                     v                        v
   packages land in .venv          cells run on ...whichever
                                   interpreter that kernel names
Activating a virtual environment changes that terminal. It does not change which Python JupyterLab 
hands your cells to. If the notebook is on the default Python 3 kernel, your code runs on system Python — 
and you get ModuleNotFoundError: No module named 'langchain' while pip list in your 
terminal shows it installed. The two facts are not in conflict; they are about two different Pythons.
SLIIT  |  Faculty of Computing  |  Department of Software Engineering Page 3 of 24
SE3090 – Software Engineering Frameworks
Lab Practical 06
Three consequences worth internalising, because they generalise far beyond this course:
1. A kernel is a process, not a setting. ipykernel install writes a small kernel.json whose 
argv[0] is an absolute path to one interpreter. Selecting a kernel selects that binary.
2. !pip install and %pip install are different.  !pip runs whatever pip the shell resolves; 
%pip installs into the interpreter running the kernel. When in doubt in a notebook, use %pip.
3. The notebook itself remembers a kernel. These lab notebooks are saved with kernelspec.name 
= "agentic-w6", so a correctly registered kernel is selected for you. If Jupyter cannot find it, it 
will ask — that prompt is a feature, not an error.
What to do: the notebook’s first cell is a kernel check. It imports nothing but the standard library, prints 
the interpreter it is running on, and — if anything is missing — prints the exact fix. Run it before anything 
else:
import importlib.util, sys
from pathlib import Path
LAB_DIR = Path.cwd()
in_a_venv = sys.prefix != sys.base_prefix
on_lab_venv = in_a_venv and \
    Path(sys.prefix).resolve() == (LAB_DIR / ".venv").resolve()
print("this kernel runs :", sys.executable)
print("on this lab .venv:", "yes" if on_lab_venv else "no")
REQUIRED = ["dotenv", "langchain", "langchain_google_genai", "langgraph", ...]
missing = [m for m in REQUIRED if importlib.util.find_spec(m) is None]
sys.prefix != sys.base_prefix is the reliable “am I in a virtual environment?” test — more 
reliable than comparing sys.executable to .venv/bin/python, because that path is often a symlink 
to the base interpreter and resolves to the same file either way.
If it reports missing packages, fix in this order:
Situation
Fix
Kernel list has “Agentic AI (week 6)”
▸
Kernel  Change Kernel…
 → select it → re-run the cell
It is not in the list
With .venv active: python -m ipykernel install -
user --name agentic-w6 --display-name "Agentic 
AI (week 6)", reload the tab
You are in VS Code
Click the kernel name at the top-right → Select Kernel → 
Python Environments… → the .venv in this folder
You want to stop thinking about it
Launch Jupyter from the venv: source 
.venv/bin/activate && python -m jupyterlab 
lab.ipynb
Nothing works and the lab is starting
%pip install -r requirements.txt in a cell, then 
restart the kernel
Page 4 of 24
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
SE3090 – Software Engineering Frameworks
Lab Practical 06
2.5  Code component: the two clients
from langchain_google_genai import (
    ChatGoogleGenerativeAI, GoogleGenerativeAIEmbeddings)
llm = ChatGoogleGenerativeAI(model=CHAT_MODEL, google_api_key=API_KEY,
                             temperature=0, timeout=60,
                             max_retries=3)
                             # free tier: back off and retry on 429
embeddings = GoogleGenerativeAIEmbeddings(model=EMBEDDING_MODEL,
                                          google_api_key=API_KEY)
Three things worth knowing about the embeddings client:
• It is called for you. Chroma invokes embed_documents on ingest and embed_query on every 
search. You will not call it directly again after the verification cell.
• Do not hard-code the dimensionality. The verification cell prints 
len(embeddings.embed_query("hello agents")); read the number off your own run rather 
than trusting a figure in a document. Gemini’s embedding models are high-dimensional and support 
truncation, so the number is a property of your configuration, not a constant of nature.
• task_type is a tuning knob you are not using yet.  Google’s embedding API can specialise vectors 
for retrieval_document versus retrieval_query. GoogleGenerativeAIEmbeddings 
accepts task_type=...; leaving it unset uses a general-purpose embedding, which is fine at this 
corpus size. Setting it is a legitimate stretch experiment — measure before and after with the Part 1 
probes.
IF YOU CHANGE EMBEDDING_MODEL, DELETE chroma_db/ AND RE-INGEST
Vectors from different models live in different spaces; comparing them produces confident nonsense, or a 
dimension-mismatch error if you are lucky.
Page 5 of 24
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
SE3090 – Software Engineering Frameworks Lab Practical 06
SECTION 3  •  TIME 0:10–0:30  •  COST APPROX. 4 REQUESTS
Part 1 — Ingest and Hybrid Search
3.1  Why this part exists
An agent that answers from its training data cannot know your refund policy, and will invent one with 
total confidence. Grounding means: retrieve the relevant text first, then answer only from it. Retrieval is 
therefore not a detail of RAG — it is RAG. Everything else is prompting.
Read data/ before you start. It is three short markdown files, and it contains a deliberate trap:
THE TRAP
Tents are returnable for 14 days, but SummitPro tent poles carry a 3-year warranty.
A customer with a snapped pole on day 20 must be told no to the return and yes to a warranty claim. 
An agent that finds only one of those facts is confidently wrong.
3.2  Code component: the vector store
from langchain_chroma import Chroma
 
vector_store = Chroma(
    collection_name=COLLECTION,
    embedding_function=embeddings,
    persist_directory=CHROMA_DIR,      # ← your "vector database" is this folder
)
• collection_name is a namespace inside the store — one database can hold several.
• embedding_function is called for you: on add_documents for each chunk, and on 
similarity_search for the query. You never manually embed.
• persist_directory makes it durable on disk. Delete the folder and the index is gone; copy it and 
the index travels.
3.3  Code component: the four ingest stages
from langchain_core.documents import Document
from langchain_text_splitters import RecursiveCharacterTextSplitter
 
 
def ingest() -> int:
    docs = [                                                   # 1. load
        Document(page_content=p.read_text(encoding="utf-8"),
                 metadata={"source": p.stem})
        for p in sorted((LAB_DIR / "data").glob("*.md"))
    ]
    splitter = RecursiveCharacterTextSplitter(                 # 2. chunk
        chunk_size=800,          # ~200 tokens: small enough to be precise
        chunk_overlap=120,       # so facts straddling a cut survive
        separators=["\n## ", "\n\n", "\n", " "],   # prefer heading/paragraph cuts
    )
    chunks = splitter.split_documents(docs)
 
    existing = vector_store.get()["ids"]
    if existing:
        vector_store.delete(ids=existing)
    vector_store.add_documents(chunks)                         # 3+4. embed & store
    return len(chunks)
SLIIT  |  Faculty of Computing  |  Department of Software Engineering Page 6 of 24
SE3090 – Software Engineering Frameworks
Lab Practical 06
Stage 1 — load. metadata={"source": p.stem} attaches the filename to every chunk. That single 
field is what makes citations like [returns-policy] possible later; metadata you fail to attach at ingest 
cannot be recovered at query time.
Stage 2 — chunk. Three parameters, three trade-offs:
Parameter
Too small
Too large
chunk_size=800
Facts get separated from the context that 
qualifies them
The embedding averages several topics 
and matches nothing precisely
chunk_overlap=12
0
A fact split across a boundary is lost from 
both halves
Duplication inflates the index and returns 
near-identical hits
separators=[...] Cuts land mid-sentence
—
The separator list is tried in order: split on markdown headings first, then blank lines, then newlines, then 
spaces. The result is chunks that mostly begin at a section heading — which is why a retrieved chunk 
reads like a coherent policy paragraph instead of a fragment.
Stages 3 and 4 — embed and store. add_documents embeds each chunk and writes text, vector and 
metadata to disk. The delete-then-add makes re-running idempotent, so a student who runs the cell 
three times ends with one clean index, not three copies.
EXPECTED OUTPUT
indexed 8 chunks into …/chroma_db
3.4  Look inside the “database”
data = vector_store.get(include=["embeddings", "documents", "metadatas"])
vec = data["embeddings"][0]
print(f"chunk 0 [{data['metadatas'][0]['source']}]:")
print(" ", data["documents"][0][:200].replace("\n", " "), "…")
print(f"  vector: dim={len(vec)}, "
      f"first 6 = {[round(float(x), 4) for x in vec[:6]]}")
Rows of (text, vector, metadata). That is the entire mystery of a vector database: a table with a 
column your code can compare numerically. The managed, paid equivalent has more scale, more security 
and an index explorer in a portal — the same three columns.
3.5  Vector search, and its two blind spots
for d in vector_store.similarity_search("can I get my money back", k=2):
    print(f"[{d.metadata['source']}] {d.page_content[:110]}…")
The refund chunk comes back although it shares almost no words with the query. Embeddings encode 
meaning, so “money back” lands near “refund”. Now the failure cases:
1. Negation. “Opened electronics are eligible” and “are NOT eligible” embed almost identically — see 
demos.ipynb for the number. Vectors capture topic far better than polarity.
2. Exact identifiers. Part codes, order numbers and product names like AquaPure are precisely where 
old-fashioned keyword matching wins.
Hence hybrid search: run both, then fuse.
3.6  Your turn — TODO 1: Reciprocal Rank Fusion
Specification. Given the vector hits and the BM25 hits for a query, produce the top k documents overall.
1. Score each document by summing 1 / (60 + rank) over every list it appears in (rank starts at 
0).
Page 7 of 24
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
SE3090 – Software Engineering Frameworks Lab Practical 06
2. Identify documents by doc.page_content[:80] so the same chunk found by both retrievers 
scores once, cumulatively.
3. Return the k highest-scoring documents.
from langchain_community.retrievers import BM25Retriever
 
_rows = vector_store.get()
bm25 = BM25Retriever.from_documents(
    [Document(page_content=t, metadata=m or {})
     for t, m in zip(_rows["documents"], _rows["metadatas"])]
)
bm25.k = 8
 
 
def hybrid_search(query: str, k: int = 3) -> list[Document]:
    """BM25 + vector search, fused with RRF."""
    vector_hits = vector_store.similarity_search(query, k=8)
    keyword_hits = bm25.invoke(query)
    ...   # ← your code
Reference implementation
def hybrid_search(query: str, k: int = 3) -> list[Document]:
    vector_hits = vector_store.similarity_search(query, k=8)
    keyword_hits = bm25.invoke(query)
 
    scores: dict[str, float] = {}
    by_key: dict[str, Document] = {}
    for hits in (vector_hits, keyword_hits):
        for rank, doc in enumerate(hits):
            key = doc.page_content[:80]           # cheap identity across both lists
            by_key[key] = doc
            scores[key] = scores.get(key, 0.0) + 1.0 / (60 + rank)
 
    top = sorted(scores, key=scores.__getitem__, reverse=True)[:k]
    return [by_key[key] for key in top]
 
 
def format_docs(docs: list[Document]) -> str:
    """Render results the way the model will see them: [source] then text."""
    return "\n\n".join(
        f"[{d.metadata.get('source', 'unknown')}]\n{d.page_content.strip()}"
        for d in docs
    ) or "No results."
 
 
def search_formatted(query: str, k: int = 3) -> str:
    return format_docs(hybrid_search(query, k))
Why RRF is written this way:
• Rank-based, not score-based. BM25 scores are unbounded term-frequency sums; cosine 
similarities sit in [-1, 1]. Adding them directly is meaningless. Using positions sidesteps the problem 
entirely — no normalisation, no tuning.
• The constant 60 comes from the original RRF paper. It damps the advantage of the very top rank, so 
a document has to do reasonably well in both lists to beat one that dominates a single list. That is 
precisely the behaviour you want from a hybrid system.
SLIIT  |  Faculty of Computing  |  Department of Software Engineering Page 8 of 24
SE3090 – Software Engineering Frameworks
Lab Practical 06
• Agreement is rewarded automatically. A chunk ranked #1 by both retrievers scores 1/60 + 1/60; 
a chunk ranked #1 by one and absent from the other scores 1/60.
• format_docs puts [source] above each chunk because that is how the model learns to cite: it 
can only cite what it can see, in the shape you show it.
3.7  Exercise 1.1 — three probes
Run hybrid_search on each and write one sentence per result:
Query
What to look for
"can I get my money back"
Pure semantic hit — the vector half earning its place
"AquaPure cartridge litres" A product identifier — the BM25 half earning its place
"do you sell kayaks"
Not in the corpus. Three chunks come back anyway, confidently 
formatted
Probe three is the important one: retrieval always returns something. Similarity is relative, never a claim 
of relevance. Judging relevance is a separate job — which is exactly what Part 3’s grader is for.
CHECKPOINT 1
Show the ingest output, your working hybrid_search, and the three probes.
Question a demonstrator may ask: why did the money-back query match the refund chunk with no 
shared keywords?
Page 9 of 24
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
SE3090 – Software Engineering Frameworks Lab Practical 06
SECTION 4  •  TIME 0:30–0:50  •  COST APPROX. 10 REQUESTS
Part 2 — Agentic RAG with create_agent
4.1  Classic RAG vs agentic RAG
Classic RAG Agentic RAG
Retrieval Always once, on the raw question The agent decides when, and with what 
query
Multi-part questions One search must cover everything Several searches, one per sub-question
Bad retrieval Answer anyway Can search again with better words
Cost One model call Several — one per decision
You are trading tokens for judgement. The mechanism is small: make retrieval a tool.
4.2  Code component: retrieval as a tool
from langchain.agents import create_agent
from langchain_core.tools import tool
 
 
@tool
def search_handbook(query: str) -> str:
    """Search the Contoso Outdoor Gear company handbook (returns policy, warranty
    guide, product FAQ). Use a short, specific search query. Call this for EVERY
    factual question about products or policies; you may call it multiple times
    with different queries for multi-part questions."""
    return search_formatted(query, k=3)
Every clause of that docstring is doing work: it states what is in the corpus (so the model can tell when a 
question is out of scope), what a good query looks like, and that repeated calls are allowed (which is 
what makes multi-hop questions work at all).
4.3  Code component: the grounding prompt
SUPPORT_PROMPT = """You are the support agent for Contoso Outdoor Gear.
 
Rules:- Answer ONLY from search_handbook results. Never rely on general knowledge about
  retail policies — Contoso's policies are unusual in places.- Cite the source of every fact in square brackets, e.g. [returns-policy].- Multi-part questions may need multiple searches with different queries.- If, after searching, the answer is not in the results, say exactly
  that and suggest
  contacting support@contoso-outdoor.example. NEVER invent policy.- Be concise and warm; lead with the answer, not the process."""
 
rag_agent = create_agent(llm, tools=[search_handbook],
                         system_prompt=SUPPORT_PROMPT)
Four instructions, four failure modes pre-empted:
Instruction Failure it prevents
“ONLY from search results… policies are unusual” The model answering from plausible-sounding general 
retail knowledge
“Cite every fact in square brackets” Unverifiable answers — citations make grounding 
checkable by a human and by an eval
SLIIT  |  Faculty of Computing  |  Department of Software Engineering Page 10 of 24
SE3090 – Software Engineering Frameworks
Failure it prevents
Lab Practical 06
Instruction
“Multi-part questions may need multiple searches”
One weak search standing in for two facts
“If not in results, say so… NEVER invent policy”
Confident fabrication when retrieval comes up empty
Note what this prompt cannot do: it cannot force grounding. It raises the probability. Week 7 shows how 
to measure whether it worked.
4.4  The three test conversations
def ask_agent(question: str) -> None:
    print("=" * 72, f"\nQ: {question}")
    for chunk in rag_agent.stream(
        {"messages": [{"role": "user", "content": question}]}, stream_mode="values"
    ):
        chunk["messages"][-1].pretty_print()
#
Question
What you are testing
1
“What’s the return window for a headlamp?”
One search, one citation, correct fact
2
“I bought a SummitPro tent 20 days ago and a pole 
snapped. Can I return it?”
The trap. Needs two searches and both facts: no 
return (14 days), yes warranty (3 years)
3
“Do you sell kayaks?”
Honesty. The only correct answer is “not in the 
handbook”
Watch the trace, not just the answer: the agent rewrites your question into a search query. That 
rewriting is agentic RAG’s first benefit, and Exercise 2.1 makes it obvious by asking the trap question 
vaguely (“my tent thing broke, what can I do?”).
CHECKPOINT 2
Show the trap-question trace with both facts found and cited, plus the kayak refusal.
Page 11 of 24
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
SE3090 – Software Engineering Frameworks Lab Practical 06
SECTION 5  •  TIME 0:50–1:20  •  COST APPROX. 12 REQUESTS  •  THE CORE OF THE WEEK
Part 3 — The Explicit Graph: Grade and Self-Correct
5.1  Why a graph
create_agent gives the model one degree of freedom: which tool to call next. It cannot express a policy 
like “grade the retrieval; if it is poor, rewrite the query and retry — at most twice, then answer honestly.” 
That is control flow, and control flow belongs in code you can read, test and draw.
LangGraph’s model: a state (a typed dict every node reads and updates), nodes (plain Python functions 
that return partial updates), and edges (fixed, or conditional on the state).
5.2  Your turn — TODO 1: the state
Specification. messages must accumulate across nodes; every other field replaces on write.
from typing import Annotated, Literal, TypedDict
from langgraph.graph.message import add_messages
 
 
class State(TypedDict):
    messages: ...        # TODO: Annotated[list, ???]
    question: str
    search_query: str
    docs: str
    retries: int
    intent_: str         # router's classification ("question" | "refund")
    relevant_: bool      # grade's verdict
    approved_: bool      # human_gate's decision (Part 5)
Reference implementation
class State(TypedDict):
    messages: Annotated[list, add_messages]   # APPEND across nodes — the reducer
    question: str
    search_query: str
    docs: str
    retries: int
    intent_: str
    relevant_: bool
    approved_: bool
 
 
MAX_RETRIES = 2
What a reducer is, and why it matters. By default, a node returning {"docs": "..."} replaces 
state["docs"]. That is right for docs, retries and search_query — you want the latest value. It is 
wrong for messages: a node returning one new message would wipe the conversation. 
Annotated[list, add_messages] tells LangGraph to merge instead: append new messages, and 
update existing ones by ID. Forget it and your history vanishes between nodes — the single most 
common Week 6 bug.
SLIIT  |  Faculty of Computing  |  Department of Software Engineering Page 12 of 24
SE3090 – Software Engineering Frameworks
Lab Practical 06
5.3  Code component: the router (given)
from pydantic import BaseModel, Field
class Intent(BaseModel):
    intent: Literal["question", "refund"] = Field(
        description="'refund' if the user is requesting a refund; else 'question'"
    )
def router(state: State) -> dict:
    result = llm.with_structured_output(Intent).invoke(
        f"Classify this customer message: {state['question']!r}"
    )
    print(f"[router] intent = {result.intent}")
    return {"search_query": state["question"], "retries": 0,
            "intent_": result.intent}
Two techniques worth naming:
• with_structured_output(Model) turns the LLM into a typed function.  You get an Intent 
instance back, not a string you must parse. Literal["question", "refund"] constrains the 
output; the description is sent to the model as part of the schema, so write it as an instruction.
• A node returns a partial update, not the whole state. Returning {"retries": 0} resets the 
counter and leaves everything else untouched.
5.4  Your turn — TODOs 2–4: retrieve, grade, decide, rewrite
Specifications.
• retrieve(state) → run search_formatted(state["search_query"]), return {"docs": 
…}, print [retrieve] query = ….
• grade(state) → use llm.with_structured_output(Grade) on the question plus the docs; 
return {"relevant_": …}.
• decide(state) → return "answer" if the docs are relevant or retries >= MAX_RETRIES; 
otherwise "rewrite".
• rewrite(state) → ask the model for one sharper search query; return {"search_query": …, 
"retries": retries + 1}.
Page 13 of 24
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
SE3090 – Software Engineering Frameworks Lab Practical 06
Reference implementation
def retrieve(state: State) -> dict:                       # TODO 2
    print(f"[retrieve] query = {state['search_query']!r}")
    return {"docs": search_formatted(state["search_query"])}
 
 
class Grade(BaseModel):
    relevant: bool = Field(
        description="True only if the documents contain enough "
        "information to answer "
        "the user's question. Identify the deciding passage before you decide."
    )
 
 
def grade(state: State) -> dict:                          # TODO 3a
    result = llm.with_structured_output(Grade).invoke(
        f"Question: {state['question']}\n\nDocuments:\n{state['docs']}\n\n"
        "Do these documents contain enough information to answer the question?"
    )
    print(f"[grade] relevant = {result.relevant}")
    return {"relevant_": result.relevant}
 
 
def decide(state: State) -> str:                          # TODO 3b
    if state["relevant_"] or state["retries"] >= MAX_RETRIES:
        return "answer"        # good docs — or give up gracefully and be honest
    return "rewrite"
 
 
def rewrite(state: State) -> dict:                        # TODO 4
    reply = llm.invoke(
        "The search query below failed to retrieve documents that "
        "answer the user's "
        "question. Write ONE sharper query for a company handbook (returns policy, "
        "warranty guide, product FAQ). Reply with the query only.\n\n"
        f"Question: {state['question']}\nFailed query: {state['search_query']}\n"
        f"Unhelpful docs (snippet): {state['docs'][:400]}"
    )
    new_query = reply.content.strip().strip('"')
    print(f"[rewrite] new query = {new_query!r}  "
          f"(retry {state['retries'] + 1}/{MAX_RETRIES})")
    return {"search_query": new_query, "retries": state["retries"] + 1}
Design notes you should be able to defend:
• The Grade description says “identify the deciding passage before you decide”. Judges that answer 
immediately drift toward “yes”; asking for the evidence first measurably sharpens them. Week 7 
uses the same trick on a bigger scale.
• decide returns a string, not a boolean. Those strings are the keys of the routing map you supply to 
add_conditional_edges, so the graph reads like a sentence: grade → answer or grade → 
rewrite.
• The retry cap is a comparison in code, never a request in a prompt. A self-correcting loop without a 
hard stop is an unbounded bill.
• rewrite is given the failed query and a snippet of the unhelpful docs — without them the model 
tends to reproduce the same query with different word order.
SLIIT  |  Faculty of Computing  |  Department of Software Engineering Page 14 of 24
SE3090 – Software Engineering Frameworks
Lab Practical 06
• .strip().strip('"') because models like to wrap a requested string in quotation marks. Small, 
real, annoying.
5.5  Code component: grounded generation (given)
def answer(state: State) -> dict:
    prompt = (
        "Answer ONLY from these handbook excerpts, citing sources "
        "like [returns-policy]. "
        "If they don't contain the answer, say so honestly.\n\n"
        f"Excerpts:\n{state['docs']}\n\nQuestion: {state['question']}"
    )
    print("[answer]")
    return {"messages": [llm.invoke(state["messages"] + [("user", prompt)])]}
state["messages"] + [("user", prompt)] sends the conversation so far plus this turn’s 
grounded instruction. That is why the follow-up question in Part 4 resolves “refunds” without you 
repeating the topic. The returned list is merged by add_messages, so history accumulates rather than 
being replaced.
5.6  Your turn — TODO 5: wire the graph
from langgraph.checkpoint.memory import InMemorySaver
from langgraph.graph import END, START, StateGraph
def build_graph():
    g = StateGraph(State)
    # add the seven nodes, then the edges of the diagram
    ...
    return g.compile(checkpointer=InMemorySaver())
Page 15 of 24
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
SE3090 – Software Engineering Frameworks Lab Practical 06
Reference implementation
def build_graph():
    g = StateGraph(State)
    g.add_node("router", router)
    g.add_node("retrieve", retrieve)
    g.add_node("grade", grade)
    g.add_node("rewrite", rewrite)
    g.add_node("answer", answer)
    g.add_node("human_gate", human_gate)
    g.add_node("process_refund", process_refund)
 
    g.add_edge(START, "router")
    g.add_conditional_edges("router", route_decision,
                            {"question": "retrieve", "refund": "human_gate"})
    g.add_edge("retrieve", "grade")
    g.add_conditional_edges("grade", decide,
                            {"answer": "answer", "rewrite": "rewrite"})
    g.add_edge("rewrite", "retrieve")   # ← the self-correction loop, one edge
    g.add_edge("answer", END)
    g.add_edge("human_gate", "process_refund")
    g.add_edge("process_refund", END)
 
    # The checkpointer gives per-thread memory AND makes interrupt/resume possible.
    return g.compile(checkpointer=InMemorySaver())
 
 
app = build_graph()
print(app.get_graph().draw_mermaid())
Call What it does
add_node(name, fn) Registers a function as a step. The name is what appears in traces.
add_edge(a, b) Unconditional: after a, always b.
add_conditional_edges(a, fn, 
mapping) After a, call fn(state) and jump to mapping[result].
add_edge("rewrite", 
"retrieve")
The self-correction loop, in one line. Cycles are legal and ordinary 
here.
compile(checkpointer=…) Produces the runnable app and attaches persistence.
draw_mermaid() prints your graph as a diagram — paste it into https://mermaid.live. If it does 
not match the picture at the top of this sheet, your edges are wrong, and you can see it rather than infer 
it.
SLIIT  |  Faculty of Computing  |  Department of Software Engineering Page 16 of 24
SE3090 – Software Engineering Frameworks
Lab Practical 06
5.7  Run it
def run(question: str, thread_id: str = "demo") -> str:
    config = {"configurable": {"thread_id": thread_id}}
    state = app.invoke(
        {"messages": [("user", question)], "question": question}, config)
    print("\nFINAL:", state["messages"][-1].content, "\n")
    return state["messages"][-1].content
run("What's the restocking fee on opened electronics?", thread_id="t1")
run("whats the deal with wet tents lol", thread_id="t2")
# vague → watch rewrite fire
The second question is deliberately sloppy so you can watch [grade] relevant = False → 
[rewrite] new query = … → a successful second retrieval. That is the whole point of the part: the 
system noticed its own bad retrieval and fixed it, without you.
CHECKPOINT 3
Show a run where the grader said irrelevant, rewrite fired, and the retry succeeded — plus your 
mermaid diagram.
Question a demonstrator may ask: which single line of your code is the self-correction loop?
Page 17 of 24
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
SE3090 – Software Engineering Frameworks
Lab Practical 06
SECTION 6  •  TIME 1:20–1:33  •  COST APPROX. 6 REQUESTS
Part 4 — Memory via Checkpointing
6.1  The mechanism
You already enabled it. Two pieces:
app = g.compile(checkpointer=InMemorySaver())            # where state is stored
config = {"configurable": {"thread_id": "alice"}}
# which conversation it belongs to
app.invoke(inputs, config)
After every node, LangGraph writes the state to the checkpointer under that thread_id. On the next 
invoke with the same id, it loads that state and merges your input into it. Memory is not a model 
capability — it is a database keyed by a conversation id.
6.2  Observe it
run("What's the return window for tents?", thread_id="alice")
run("And how long do refunds take to arrive?", thread_id="alice")   # no re-asking
run("And how long do refunds take to arrive?", thread_id="stranger")  # no context
Same follow-up, two threads, two different behaviours. Nothing about the model changed.
6.3  Inspect what is stored
snapshot = app.get_state({"configurable": {"thread_id": "alice"}})
print("stored fields :", sorted(snapshot.values))
print("messages      :", len(snapshot.values["messages"]))
for m in snapshot.values["messages"]:
    print(f"  {type(m).__name__:<13} {str(m.content)[:80]}")
get_state returns the full snapshot — every field of State, not only messages. Two consequences 
worth stating out loud:
• Memory is inspectable and editable. You can read it, log it, redact it, or write a corrected value 
back. Contrast that with “the model remembers”, which offers no such handle.
• InMemorySaver dies with the process.  Swap in SqliteSaver and the same code survives a 
restart. That is stretch goal 4, and it is one line.
CHECKPOINT 4
Show the follow-up working on alice and failing on stranger, plus the thread snapshot.
Page 18 of 24
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
SE3090 – Software Engineering Frameworks Lab Practical 06
SECTION 7  •  TIME 1:33–1:45  •  COST APPROX. 4 REQUESTS
Part 5 — Human-in-the-Loop Refunds
7.1  Why interrupt is not input()
Money is the classic place to stop and ask a person. A naive implementation blocks on input() — which 
requires the process to stay alive, holds a thread, and dies with the tab. LangGraph’s interrupt() does 
something categorically different: it checkpoints the entire graph state and ends the run. The process 
may exit. Hours may pass. Resuming carries only the decision.
7.2  Your turn — TODO 6: the gate
def human_gate(state: State) -> dict:
    # decision = interrupt({...})   ← one line freezes the workflow
    ...
 
 
app = build_graph()      # rebuild so the graph uses your new human_gate
Reference implementation
from langgraph.types import Command, interrupt
 
 
def human_gate(state: State) -> dict:
    decision = interrupt(
        {"ask": f"Refund requested: {state['question']!r}. Approve?"})
    return {"approved_": str(decision).lower().startswith("approve")}
• The dict passed to interrupt(...) is the payload delivered to whoever must decide — put 
everything a reviewer needs in it, because they will not have your Python session.
• When resumed, interrupt(...) returns the value supplied by Command(resume=…), and the 
node runs again from the top. Keep gate nodes free of side effects before the interrupt: anything 
above that line executes twice.
• You must rebuild the graph after redefining the function, or the compiled app keeps the old one. 
(Symptom: no pause happens.)
7.3  Driving the pause and the resume
config = {"configurable": {"thread_id": "refund-1"}}
question = "I want a refund for my NightHawk headlamp, order #4417"
 
state = app.invoke(
    {"messages": [("user", question)], "question": question}, config)
 
if state.get("__interrupt__"):
    print("PAUSED — the graph is frozen in the checkpointer.")
    print("payload:", state["__interrupt__"][0].value)
 
state = app.invoke(Command(resume="approve"), config)     # a human decides
print("FINAL:", state["messages"][-1].content)
• A paused run returns a state containing __interrupt__; [0].value is your payload.
• Command(resume="approve") is the entire second input. No question, no history — the graph 
already has all of that. The config’s thread_id is what locates it.
• Repeat on a fresh thread with resume="deny" and confirm process_refund takes the other 
branch. A gate that cannot say no is not a gate.
SLIIT  |  Faculty of Computing  |  Department of Software Engineering Page 19 of 24
SE3090 – Software Engineering Frameworks
Lab Practical 06
7.4  The dependency worth stating
Compile without a checkpointer and interrupt() raises. There is nowhere to freeze state, so there is 
nothing to resume. Part 5 is impossible without Part 4 — try it once; the error message is the lesson.
CHECKPOINT 5
Show one approved and one denied refund flow, including the interrupt payloads.
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
Page 20 of 24
SE3090 – Software Engineering Frameworks Lab Practical 06
SECTION 8  •  TIME 1:45–1:55  •  COST APPROX. 8 REQUESTS
Part 6 — The Workflow Behind FastAPI
8.1  Why this part exists
A human gate inside a notebook is a demo. The real question is whether the pause survives being 
packaged: can the workflow start in one HTTP request, wait for a person, and finish in another? It can — 
because the state is in the checkpointer, not in a call stack.
Endpoint Purpose
GET /health Service + index status; no model call
POST /ingest Rebuild the index from ./data
GET /search?q=…&k=3 Raw hybrid search, no agent
POST /ask Run the graph on a thread; returns an answer or awaiting_approval
POST /resume A human’s decision; the graph continues
GET /threads/{id} What the checkpointer holds for that thread
8.2  Code component: the response contract
class GraphResponse(BaseModel):
    status: Literal["completed", "awaiting_approval"]
    answer: str | None = None
    interrupt: dict[str, Any] | None = None
    nodes: list[str] = Field(default_factory=list,
                             description="Nodes that ran, in order.")
    thread_id: str
status is the field a caller branches on: render an answer, or render an approval screen. A workflow that 
can pause must say so in its type, not in prose the client has to parse.
8.3  Code component: running the graph and reporting the trajectory
def _run(inputs: Any, thread_id: str) -> GraphResponse:
    """Stream the graph so we can report which nodes ran — the trajectory."""
    config = {"configurable": {"thread_id": thread_id}}
    nodes: list[str] = []
    paused: dict[str, Any] | None = None
 
    for chunk in GRAPH.stream(inputs, config, stream_mode="updates"):
        for node, update in chunk.items():
            if node == "__interrupt__":
                paused = dict(update[0].value)
            else:
                nodes.append(node)
 
    if paused is not None:
        return GraphResponse(status="awaiting_approval", interrupt=paused,
                             nodes=nodes, thread_id=thread_id)
 
    values = GRAPH.get_state(config).values
    final = values["messages"][-1].content if values.get("messages") else ""
    return GraphResponse(status="completed", answer=final,
                         nodes=nodes, thread_id=thread_id)
• stream_mode="updates" yields {node_name: update} after each node, which is how nodes 
becomes an ordered execution trace — the cheap observability that makes “why did it answer 
that?” answerable.
SLIIT  |  Faculty of Computing  |  Department of Software Engineering Page 21 of 24
SE3090 – Software Engineering Frameworks
Lab Practical 06
• An interrupt arrives as the reserved key "__interrupt__", so it is filtered out of the node list and 
turned into the pause payload.
• After a completed run, the answer is read from get_state(config) — the same checkpointer the 
pause would have used. One source of truth for both paths.
• _run is shared by /ask and /resume; the only difference between them is the input 
({"messages": …} versus Command(resume=…)).
8.4  The pause/resume pair, over HTTP
curl -s -X POST localhost:8000/ask \
  -H 'content-type: application/json' \
  -d '{"question":"I want a refund for order #4417","thread_id":"demo"}'
# → {"status":"awaiting_approval",
#    "interrupt":{"ask":"Refund requested: …"},"nodes":["router"],…}
curl -s -X POST localhost:8000/resume \
  -H 'content-type: application/json' \
  -d '{"thread_id":"demo","decision":"approve"}'
# → {"status":"completed","answer":"Refund approved and queued …",
#    "nodes":["human_gate","process_refund"],…}
Between those two commands the workflow is frozen state, not a blocked thread. No connection is held 
open; nothing about the original question is resent.
8.5  Exercise 6.1
Inspect GET /threads/web-user-7, then answer:
(a)  Another customer calls /ask with thread_id="web-user-7". What do they see, and why is 
thread_id therefore security-relevant?
(b)  The server restarts while a refund awaits approval. What is lost, and which one-line change fixes it?
Expected answers
(a)  They read and continue that customer’s conversation, including whatever the transcript contains. 
thread_id is an authorization boundary: derive it from an authenticated session, never accept it 
verbatim from the client, and never make it guessable or sequential.
(b)  Everything in InMemorySaver — including refunds frozen mid-approval, which vanish silently. The 
fix is a durable checkpointer:
from langgraph.checkpoint.sqlite import SqliteSaver
with SqliteSaver.from_conn_string("checkpoints.sqlite") as saver:
    GRAPH = build_graph_with(saver)
Same interrupt, same resume, now surviving a restart.
CHECKPOINT 6
Show two-turn memory over HTTP and one full /ask → /resume refund cycle.
Page 22 of 24
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
SE3090 – Software Engineering Frameworks
Lab Practical 06
SECTION 9
Wrap-Up, Deliverables, Stretch Goals
Today’s agent is grounded, self-correcting, stateful, supervised, and deployable. That is a production
shaped agent. What remains for Week 7: scale-out (multiple agents), proof (evaluation), and armour 
(security).
DELIVERABLES
lab6_evidence.txt with the six checkpoints, plus lab.ipynb with Parts 1, 3 and 5 completed.
Stretch goals
1. Summarization node — after 8+ messages on a thread, compress older turns into a summary 
message. Verify tokens drop on turn 9.
2. Citation checker — a node after answer that verifies every [source] cited actually appears in 
docs, and flags the answer if not. A groundedness guard, and a preview of Week 7.
3. Parent-document retrieval — re-ingest with small chunks carrying a parent metadata field holding 
the full section; retrieve small, feed the model big.
4. Durable state — swap InMemorySaver for SqliteSaver, start a refund via the API, kill the server, 
restart, then POST /resume.
5. Streaming answers — add POST /ask/stream that yields node updates as server-sent events.
Page 23 of 24
SLIIT  |  Faculty of Computing  |  Department of Software Engineering
SE3090 – Software Engineering Frameworks
Lab Practical 06
SECTION 10
Troubleshooting
Symptom
Cause → fix
“Index is empty” / no results
Ingest cell not run, or chroma_db/ deleted — re-run Part 1
Only 1–2 chunks ever returned
Check k, and that ingest reported ~8 chunks
History disappears between nodes
Missing add_messages reducer on State["messages"]
interrupt raises about a checkpointer
You compiled without checkpointer=
The interrupt never fires
Graph not rebuilt after implementing human_gate
Grader always says relevant
Grading prompt too soft — require the deciding passage first
Embedding call fails
Check EMBEDDING_MODEL=gemini-embedding-001 and that the 
key is valid
Dimension-mismatch from Chroma
429 / ResourceExhausted
You changed embedding models — delete chroma_db/ and re
ingest
Free-tier rate limit: wait ~60 s, or set CHAT_MODEL=gemini-2.5
flash-lite
The Part 6 cell hangs
An old server thread is still bound — restart the kernel
Section 11 — Glossary
Term
Meaning as used in this lab
Chunk
A slice of a document, sized so its embedding represents one idea.
Embedding
A vector encoding meaning; nearby vectors mean similar things.
BM25
Classic keyword ranking — strong on exact terms and identifiers.
RRF
Reciprocal Rank Fusion: combining ranked lists by 1/(60+rank).
Hybrid search
Keyword + vector retrieval, fused. Beats either half alone.
Grounding
Answering only from retrieved text, with citations.
Agentic RAG
Retrieval exposed as a tool, so the agent decides when and what to search.
State
The typed dict every node reads and updates.
Reducer
The merge rule for a state field; add_messages appends instead of replacing.
Conditional edge
A routing function whose return value selects the next node.
Checkpointer
Storage for graph state, keyed by thread_id. Provides memory and resumability.
Interrupt
A pause that checkpoints state and ends the run, resumed later with 
Command(resume=…).