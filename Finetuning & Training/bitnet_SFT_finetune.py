import os
import sys
import json
import logging
import random
import numpy as np
import torch
import tqdm
import datasets
from datasets import load_dataset
from torch.utils.data import DataLoader, Dataset
import transformers
from transformers import AutoModelForCausalLM, AutoTokenizer
from zeta.optim import StableAdamWUnfused
import pkg_resources
import sys

# Suppress TorchDynamo errors (this will fallback to eager mode)
import torch._dynamo
torch._dynamo.config.suppress_errors = True

##################
# Data Processing
##################
def formatting_func(example):
    """
    Formats an example using the new style.
    """
    text = f"### Question: {example['Instruction']}\n ### Answer: {example['Output']}"
    return text

# ---------------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------------
NUM_BATCHES = 1000
BATCH_SIZE = 1
GRADIENT_ACCUMULATE_EVERY = 4
LEARNING_RATE = 2e-4
VALIDATE_EVERY = 5
GENERATE_EVERY = 250
GENERATE_LENGTH = 512

###############
# Setup logging
###############
logging.basicConfig(
    format="%(asctime)s - %(levelname)s - %(name)s - %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
    handlers=[logging.StreamHandler(sys.stdout)],
)
log_level = 1

datasets.utils.logging.set_verbosity(log_level)
transformers.utils.logging.set_verbosity(log_level)
transformers.utils.logging.enable_default_handler()
transformers.utils.logging.enable_explicit_format()

# ---------------------------------------------------------------------------------
# Load Hugging Face model and tokenizer
# ---------------------------------------------------------------------------------
model_id = "microsoft/bitnet-b1.58-2B-4T-bf16"
tokenizer = AutoTokenizer.from_pretrained(model_id, use_fast=False)
model = AutoModelForCausalLM.from_pretrained(
    model_id,
    torch_dtype=torch.bfloat16
)
hf_save_dir = "./bitnet"

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
if torch.cuda.is_available():
    print("CUDA is available. Using GPU:", torch.cuda.get_device_name(0))
else:
    print("CUDA not available; using CPU.")
model.to(device)
print(f"Loaded pre-trained Hugging Face model '{model_id}'.")

# ---------------------------------------------------------------------------------
# Load new Hugging Face dataset and preprocess it using the new formatting_func
# ---------------------------------------------------------------------------------
# Load the dataset from Hugging Face
full_dataset = load_dataset("Bifrost-AI/Solana-Vanguard-Challenge", split="train")

def preprocess_function(example):
    # Format the example using the new formatting function.
    formatted_text = formatting_func(example)
    
    # Determine the prompt portion by looking for the answer marker.
    answer_marker = "### Answer:"
    if answer_marker in formatted_text:
        # Include the answer marker in the prompt.
        prompt_text = formatted_text.split(answer_marker, 1)[0] + answer_marker
    else:
        prompt_text = formatted_text

    # Tokenize the full formatted text.
    tokenized_full = tokenizer(formatted_text, truncation=True, padding=False)
    # Tokenize only the prompt portion to measure its token length.
    tokenized_prompt = tokenizer(prompt_text, truncation=True, padding=False)
    prompt_len = len(tokenized_prompt["input_ids"])
    
    input_ids = tokenized_full["input_ids"]
    labels = input_ids.copy()
    # Mask the prompt tokens (loss computed only on answer tokens)
    for i in range(prompt_len):
        labels[i] = -100

    return {
        "input_ids": torch.tensor(input_ids, dtype=torch.long),
        "labels": torch.tensor(labels, dtype=torch.long),
        "prompt_len": prompt_len
    }

# Apply preprocessing and remove the original columns.
processed_dataset = full_dataset.map(preprocess_function, remove_columns=full_dataset.column_names)

# Set the format so that when the dataset is indexed, the fields are torch tensors.
processed_dataset.set_format(type="torch", columns=["input_ids", "labels", "prompt_len"])

# Split the processed dataset into train and validation sets (90/10 split).
split_idx = int(0.9 * len(processed_dataset))
train_dataset = torch.utils.data.Subset(processed_dataset, list(range(0, split_idx)))
val_dataset = torch.utils.data.Subset(processed_dataset, list(range(0, split_idx)))

# ---------------------------------------------------------------------------------
# Collate function for DataLoader
# ---------------------------------------------------------------------------------
def sft_collate_fn(batch):
    """
    Collate a list of examples by padding them to the maximum sequence length in the batch.
    """
    max_len = max(x["input_ids"].size(0) for x in batch)
    input_ids_batch = []
    labels_batch = []
    prompt_lens = []
    pad_id = tokenizer.pad_token_id if tokenizer.pad_token_id is not None else 0
    for ex in batch:
        input_ids = ex["input_ids"]
        labels = ex["labels"]
        pad_len = max_len - input_ids.size(0)
        input_ids_padded = torch.cat([input_ids, torch.full((pad_len,), pad_id, dtype=input_ids.dtype)])
        labels_padded = torch.cat([labels, torch.full((pad_len,), -100, dtype=labels.dtype)])
        input_ids_batch.append(input_ids_padded)
        labels_batch.append(labels_padded)
        prompt_lens.append(ex["prompt_len"])
    return {"input_ids": torch.stack(input_ids_batch), "labels": torch.stack(labels_batch), "prompt_len": prompt_lens}

def cycle(loader):
    while True:
        yield from loader

train_loader = cycle(DataLoader(train_dataset, batch_size=BATCH_SIZE, shuffle=True, collate_fn=sft_collate_fn))
val_loader = cycle(DataLoader(val_dataset, batch_size=BATCH_SIZE, shuffle=False, collate_fn=sft_collate_fn))

# ---------------------------------------------------------------------------------
# Setup optimizer
# ---------------------------------------------------------------------------------
optim = StableAdamWUnfused(model.parameters(), lr=LEARNING_RATE)

# ---------------------------------------------------------------------------------
# Training loop for SFT fine tuning.
#
# For Hugging Face causal LM models, supplying 'labels' automatically shifts inputs
# and computes the loss only on the unmasked portion (i.e. the answer tokens).
# ---------------------------------------------------------------------------------
for i in tqdm.tqdm(range(NUM_BATCHES), mininterval=10.0, desc="training"):
    model.train()
    total_loss = 0.0
    for _ in range(GRADIENT_ACCUMULATE_EVERY):
        batch = next(train_loader)
        input_ids = batch["input_ids"].to(device)
        labels = batch["labels"].to(device)
        
        outputs = model(input_ids=input_ids, labels=labels)
        loss = outputs.loss
        loss.backward()
        total_loss += loss.item()
    
    print(f"training loss: {total_loss / GRADIENT_ACCUMULATE_EVERY}")
    torch.nn.utils.clip_grad_norm_(model.parameters(), 0.5)
    optim.step()
    optim.zero_grad()
    

# ---------------------------------------------------------------------------------
# Save the final fine-tuned model after training.
# ---------------------------------------------------------------------------------
output_checkpoint = "solana-bitnet.pt"
torch.save(model.state_dict(), output_checkpoint)
model.save_pretrained(hf_save_dir)
tokenizer.save_pretrained(hf_save_dir)
print(f"Model saved to '{output_checkpoint}' and Hugging Face artifacts saved to '{hf_save_dir}'!")