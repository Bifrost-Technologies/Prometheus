import React, { useState, FormEvent } from 'react';

export interface PromptFormProps {
    onSubmit: (prompt: string) => void;
}

export const PromptForm: React.FC<PromptFormProps> = ({ onSubmit }) => {
    const [prompt, setPrompt] = useState('');

    const handleSubmit = (e: FormEvent) => {
        e.preventDefault();
        if (!prompt.trim()) return;
        onSubmit(prompt);
    };

    return (
        <form onSubmit={handleSubmit}>
            <textarea
                value={prompt}
                onChange={e => setPrompt(e.target.value)}
                placeholder="Describe your Solana program..."
                rows={6}
                style={{ width: '100%' }}
            />
            <button type="submit">Generate Code</button>
        </form>
    );
};