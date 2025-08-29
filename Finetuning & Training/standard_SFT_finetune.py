import sys
import logging

import datasets
from datasets import load_dataset
import torch
import transformers
from trl import SFTTrainer
from transformers import AutoModelForCausalLM, AutoTokenizer, TrainingArguments

logger = logging.getLogger(__name__)

###################
# Hyper-parameters
###################
training_config = {
    "bf16": True,
    "do_eval": False,
    "learning_rate": 2e-5,
    "log_level": "info",
    "logging_steps": 20,
    "logging_strategy": "steps",
    "lr_scheduler_type": "cosine",
    "num_train_epochs": 1,
    "max_steps": 500,
    "output_dir": "./nemotronOpenCodeReasoning7B",
    "overwrite_output_dir": True,
    "per_device_eval_batch_size": 1,
    "per_device_train_batch_size": 1,
    "remove_unused_columns": True,
    "save_steps": 500,
    "save_total_limit": 1,
    "seed": 0,
    "gradient_checkpointing": True,
    "gradient_checkpointing_kwargs":{"use_reentrant": False},
    "gradient_accumulation_steps": 1,
    "warmup_ratio": 0.2,
    }

train_conf = TrainingArguments(**training_config)

###############
# Setup logging
###############
logging.basicConfig(
    format="%(asctime)s - %(levelname)s - %(name)s - %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
    handlers=[logging.StreamHandler(sys.stdout)],
)
log_level = train_conf.get_process_log_level()
logger.setLevel(log_level)
datasets.utils.logging.set_verbosity(log_level)
transformers.utils.logging.set_verbosity(log_level)
transformers.utils.logging.enable_default_handler()
transformers.utils.logging.enable_explicit_format()

# Log on each process a small summary
logger.warning(
    f"Process rank: {train_conf.local_rank}, device: {train_conf.device}, n_gpu: {train_conf.n_gpu}"
    + f" distributed training: {bool(train_conf.local_rank != -1)}, 16-bits training: {train_conf.fp16}"
)
logger.info(f"Training/evaluation parameters {train_conf}")


################
# Model Loading
################
checkpoint_path = "Qwen/Qwen3-Coder-30B-A3B-Instruct"
model_kwargs = dict(
    use_cache=False,
    trust_remote_code=True,
    torch_dtype=torch.bfloat16,
    device_map="cuda:0"
)
model = AutoModelForCausalLM.from_pretrained(checkpoint_path, **model_kwargs)
tokenizer = AutoTokenizer.from_pretrained(checkpoint_path)
tokenizer.model_max_length = 2043
# tokenizer.pad_token = tokenizer.unk_token  # use unk rather than eos token to prevent endless generation
# tokenizer.pad_token_id = tokenizer.convert_tokens_to_ids(tokenizer.pad_token)
# tokenizer.padding_side = 'right'


##################
# Data Processing
##################
def formatting_func(example):
    text = f"### Question: {example['Instruction']}\n ### Answer: {example['Output']}"
    return text


train_dataset = load_dataset("Bifrost-AI/Solana-Vanguard-Challenge", split="train")
column_names = list(train_dataset.features)

#processed_train_dataset = train_dataset.map(
#    apply_chat_template,
#    fn_kwargs={"tokenizer": tokenizer},
#    num_proc=1,
#    remove_columns=column_names,
#    desc="Applying chat template to train_sft",
#)

#processed_test_dataset = test_dataset.map(
#    apply_chat_template,
#    fn_kwargs={"tokenizer": tokenizer},
#    num_proc=1,
#    remove_columns=column_names,
#    desc="Applying chat template to test_sft",
#)


###########
# Training
###########
trainer = SFTTrainer(
    model=model,
    args=train_conf,
    #peft_config=peft_conf,
    train_dataset=train_dataset,
    formatting_func=formatting_func,
    eval_dataset=train_dataset,
    #processing_class=tokenizer,
    #packing=True
)
with torch.backends.cuda.sdp_kernel(enable_flash=True, enable_math=False, enable_mem_efficient=False):
    train_result = trainer.train()

metrics = train_result.metrics
trainer.log_metrics("train", metrics)
trainer.save_metrics("train", metrics)
trainer.save_state()


#############
# Evaluation
#############
tokenizer.padding_side = 'left'
metrics = trainer.evaluate()
metrics["eval_samples"] = len(train_dataset)
trainer.log_metrics("eval", metrics)
trainer.save_metrics("eval", metrics)


# ############
# # Save model
# ############
trainer.save_model(train_conf.output_dir)
