// src/components/FileBrowser.tsx
import React, { useState } from 'react'
import JSZip from 'jszip'
import { saveAs } from 'file-saver'
import {
    Box,
    Paper,
    List,
    ListItem,
    ListItemButton,
    ListItemText,
    Typography,
    Button
} from '@mui/material'

export interface BlueprintFile {
    id: string
    content: string
}

export interface FileBrowserProps {
    files: BlueprintFile[]
}

export const FileBrowser: React.FC<FileBrowserProps> = ({ files }) => {
    const [selected, setSelected] = useState<BlueprintFile>(files[0])
    const [isZipping, setIsZipping] = useState(false)

    // Generates the zip and starts the download
    const handleGenerateProject = async () => {
        setIsZipping(true)
        try {
            const zip = new JSZip()

            // 1. Add template root files
            const templateRootFiles = ['Cargo.toml', 'Cargo.lock', 'build.rs', 'LICENSE']
            await Promise.all(
                templateRootFiles.map(async (name) => {
                    const res = await fetch(`/template/${name}`)
                    const text = await res.text()
                    zip.file(name, text)
                })
            )

            // 2. Create src folder and subfolders
            const src = zip.folder('src')!
            // fetch lib.rs from template/src
          //  {
             //   const res = await fetch('/template/src/lib.rs')
            //    const text = await res.text()
            //    src.file('lib.rs', text)
           // }
           // src.folder('instructions')
           // src.folder('state')

            // 3. Add each BlueprintFile
            files.forEach((file) => {
                // If your IDs include subfolder (e.g. "state/foo.rs"), split them
                const parts = file.id.split('/')
                if (parts.length > 1) {
                    const folder = src.folder(parts[0])!
                    folder.file(parts.slice(1).join('/'), file.content)
                } else {
                    src.file(file.id, file.content)
                }
            })

            // 4. Generate zip blob & download
            const blob = await zip.generateAsync({ type: 'blob' })
            saveAs(blob, 'project.zip')
        } catch (err) {
            console.error('Failed to generate ZIP', err)
        } finally {
            setIsZipping(false)
        }
    }

    return (
        <>
            {/* Download Button */}
            <Box sx={{ textAlign: 'right', mb: 1 }}>
                <Button
                    variant="contained"
                    color="primary"
                    onClick={handleGenerateProject}
                    disabled={isZipping}
                >
                    {isZipping ? 'Preparing ZIP…' : 'Download Project'}
                </Button>
            </Box>

            {/* File Browser Layout */}
            <Box display="flex" height="100%">
                {/* Sidebar */}
                <Paper
                    elevation={3}
                    sx={{
                        width: 240,
                        height: '100%',
                        overflowY: 'auto',
                        mr: 2,
                        backgroundColor: 'rgba(255, 255, 255, 0.15)',
                        backdropFilter: 'blur(10px)',
                        WebkitBackdropFilter: 'blur(10px)',
                        border: '1px solid rgba(255, 255, 255, 0.3)',
                        boxShadow: '0 8px 32px rgba(0, 0, 0, 0.12)',
                        borderRadius: 2,
                    }}
                >
                    <List disablePadding>
                        {files.map((file) => (
                            <ListItem key={file.id} disablePadding>
                                <ListItemButton
                                    selected={file.id === selected.id}
                                    onClick={() => setSelected(file)}
                                >
                                    <ListItemText primary={file.id} />
                                </ListItemButton>
                            </ListItem>
                        ))}
                    </List>
                </Paper>

                {/* Content Panel */}
                <Paper
                    elevation={3}
                    sx={{
                        flexGrow: 1,
                        p: 2,
                        display: 'flex',
                        flexDirection: 'column',
                        overflow: 'auto',
                        backgroundColor: 'rgba(255, 255, 255, 0.15)',
                        backdropFilter: 'blur(10px)',
                        WebkitBackdropFilter: 'blur(10px)',
                        border: '1px solid rgba(255, 255, 255, 0.3)',
                        boxShadow: '0 8px 32px rgba(0, 0, 0, 0.12)',
                        borderRadius: 2,
                    }}
                >
                    <Typography variant="h6" gutterBottom>
                        {selected.id}
                    </Typography>
                    <Box
                        component="pre"
                        sx={{
                            backgroundColor: 'rgba(24, 24, 24, 0.4)',
                            backdropFilter: 'blur(10px)',
                            WebkitBackdropFilter: 'blur(10px)',
                            border: '1px solid rgba(255, 255, 255, 0.3)',
                            boxShadow: '0 8px 32px rgba(0, 0, 0, 0.12)',
                            color: 'text.primary',
                            p: 2,
                            borderRadius: 1,
                            fontFamily: 'monospace',
                            whiteSpace: 'pre-wrap',
                            flexGrow: 1,
                            overflowX: 'auto',
                        }}
                    >
                        {selected.content}
                    </Box>
                </Paper>
            </Box>
        </>
    )
}