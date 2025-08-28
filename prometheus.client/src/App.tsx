// src/App.tsx
import React, { useState, useEffect } from 'react'
import {
    Typography,
    Container,
    Card,
    CardContent,
    Box,
    TextField,
    Button,
    CircularProgress,
    Alert
} from '@mui/material'
import { requestBlueprint, checkStatus, getBlueprint } from './services/api'
import { FileBrowser } from './components/FileBrowser'

export interface BlueprintFile {
    id: string;
    content: string;
}

// Wallet Demo Component
function App() {
    const [promptInput, setPromptInput] = useState('')
    const [jobId, setJobId] = useState<string | null>(null)
    const [status, setStatus] = useState<'Pending' | 'Complete' | null>(null)
    const [blueprint, setBlueprint] = useState<string | null>(null)
    const [files, setFiles] = useState<BlueprintFile[]>([])
    const [error, setError] = useState<string | null>(null)

    // Utility to extract file names from the <Files> block
    function parseFilesBlock(raw: string): string[] {
        const filesMatch = raw.match(/<Files>([\s\S]*?)<\/Files>/i)
        if (!filesMatch) return []
        const segment = filesMatch[1]
        const names: string[] = []

        segment.split(',')
            .map(s => s.trim())
            .filter(Boolean)
            .forEach(n => names.push(n))

        const tagPattern = /<(Root|State|Instructions)>([\s\S]*?)<\/\1>/gi
        let m: RegExpExecArray | null
        while ((m = tagPattern.exec(segment))) {
            m[2]
                .split(',')
                .map(s => s.trim())
                .filter(Boolean)
                .forEach(n => names.push(n))
        }
        return names
    }

    // Utility to build BlueprintFile[] from raw blueprint text
    function parseBlueprintFiles(raw: string): BlueprintFile[] {
        const fileNames = parseFilesBlock(raw)
        return fileNames.map(name => {
            // escape dots and other regex meta-characters
            const esc = name.replace(/[-\/\\^$*+?.()|[\]{}]/g, '\\$&')
            const regex = new RegExp(`<${esc}>([\\s\\S]*?)<\\/${esc}>`, 'i')
            const match = raw.match(regex)
            return {
                id: name,
                content: match ? match[1].trim() : ''
            }
        })
    }

    // Poll the job status and, when complete, fetch + parse
    useEffect(() => {
      
        if (!jobId || status === 'Complete') return

        const interval = setInterval(async () => {
            try {
                const newStatus = await checkStatus(jobId)
                setStatus(newStatus)

                if (newStatus === 'Complete') {
                    clearInterval(interval)
                    const raw = await getBlueprint(jobId)
                    setBlueprint(raw)
                    setFiles(parseBlueprintFiles(raw))
                }
            } catch (e) {
                console.error(e)
                clearInterval(interval)
                setError('Something went wrong')
            }
        }, 2000)

        return () => clearInterval(interval)
    }, [jobId, status])

    const handleGenerate = async (e: React.FormEvent) => {
        e.preventDefault()
        setJobId(null)
        setStatus(null)
        setBlueprint(null)
        setFiles([])
        setError(null)

        const { jobId: newJobId } = await requestBlueprint(promptInput)
        setJobId(newJobId)
        setStatus('Pending')
    }

    const isLoading =
        status === 'Pending' ||
        (status === 'Complete' && blueprint === null)

    return (
        <>
           
            <Box
                sx={{
                    minHeight: '100vh',
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    justifyContent: 'flex-start',
                    px: 2,
                    pt: 4,
                  
                }}
            >

                <Container maxWidth={false} sx={{
                    maxWidth: 1200, mt: 4, mb: 4
}}>
                    {/* Prompt Form */}
                    {!jobId && (
                        <Card elevation={3} sx={{
                            width: '100%', backgroundColor: 'rgba(255, 255, 255, 0.15)', // translucent white
                            backdropFilter: 'blur(10px)',                 // blur background behind
                            WebkitBackdropFilter: 'blur(10px)',           // Safari support
                            border: '1px solid rgba(255, 255, 255, 0.3)',  // faint glass edge
                            boxShadow: '0 8px 32px rgba(0, 0, 0, 0.12)',    // soft drop-shadow
                            borderRadius: 2,
}}>
                            <CardContent>
                                <Typography variant="h5" gutterBottom color="white">
                                    Enter Your Prompt
                                </Typography>

                                <Box
                                    component="form"
                                    onSubmit={handleGenerate}
                                    sx={{
                                        display: 'flex',
                                        gap: 2,
                                        alignItems: 'center',
                                        backgroundColor: 'transparent', // translucent white
                                     
                                    }}
                                >
                                    <TextField
                                        fullWidth
                                        multiline
                                        rows={6}
                                        placeholder="Describe what you need..."
                                        value={promptInput}
                                        onChange={e => setPromptInput(e.target.value)}
                                        sx={{
                                            display: 'flex',
                                            gap: 2,
                                            alignItems: 'center',
                                            backgroundColor: 'rgba(25, 25, 25, 0.15)', // translucent white
                                            backdropFilter: 'blur(10px)',                 // blur background behind
                                            WebkitBackdropFilter: 'blur(10px)',           // Safari support
                                            border: '1px solid rgba(255, 255, 255, 0.3)',  // faint glass edge
                                            boxShadow: '0 8px 32px rgba(0, 0, 0, 0.12)',    // soft drop-shadow
                                            borderRadius: 2,
                                        }}
                                    />

                                    <Button type="submit" variant="contained" size="large" sx={{
                                    
                                        backgroundColor: 'rgba(255, 255, 255, 0.15)', // translucent white
                                        backdropFilter: 'blur(10px)',                 // blur background behind
                                        WebkitBackdropFilter: 'blur(10px)',           // Safari support
                                        border: '1px solid rgba(255, 255, 255, 0.3)',  // faint glass edge
                                        boxShadow: '0 8px 32px rgba(0, 0, 0, 0.12)',    // soft drop-shadow
                                        borderRadius: 2,
                                    }}>
                                        Generate
                                    </Button>
                                </Box>
                            </CardContent>
                        </Card>
                    )}

                    {/* Loading Spinner */}
                    {isLoading && (
                        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
                            <CircularProgress size={48} />
                        </Box>
                    )}

                    {/* Error Message */}
                    {error && (
                        <Alert severity="error" sx={{ mt: 4 }}>
                            {error}
                        </Alert>
                    )}

                    {/* File Browser */}
                    {files.length > 0 && (
                        <Card elevation={3} sx={{ mt: 4, height: 600, backgroundColor: 'transparent' }}>
                            <CardContent sx={{ p: 0, height: '100%', backgroundColor: 'transparent' }}>
                                <FileBrowser files={files} sx={{
                                    background: 'transparent', backgroundColor: 'rgba(25, 25, 25, 0.15)', // translucent white
                                    backdropFilter: 'blur(10px)',                 // blur background behind
                                    WebkitBackdropFilter: 'blur(10px)',           // Safari support
                                    border: '1px solid rgba(255, 255, 255, 0.3)',  // faint glass edge
                                    boxShadow: '0 8px 32px rgba(0, 0, 0, 0.12)',    // soft drop-shadow
                                    borderRadius: 2,
}} />
                            </CardContent>
                        </Card>
                    )}

                    {/* (Optional) Raw Debug Output */}
                    {blueprint && files.length === 0 && (
                        <Card elevation={3} sx={{ mt: 4 }}>
                            <CardContent>
                                <Typography variant="h6" gutterBottom>
                                    Raw Blueprint
                                </Typography>
                                <Box
                                    component="pre"
                                    sx={{
                                        backgroundColor: 'background.paper',
                                        color: 'text.primary',
                                        p: 2,
                                        borderRadius: 1,
                                        fontFamily: 'monospace',
                                        whiteSpace: 'pre-wrap'
                                    }}
                                >
                                    {blueprint}
                                </Box>
                            </CardContent>
                        </Card>
                    )}
                </Container>
             
            </Box>
        </>
    )
}

export default App