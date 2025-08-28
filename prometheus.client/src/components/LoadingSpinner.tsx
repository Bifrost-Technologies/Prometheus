import React from 'react';
import { PulseLoader } from 'react-spinners';

export const LoadingSpinner: React.FC = () => (
    <div style={{ textAlign: 'center', marginTop: '2rem' }}>
        <PulseLoader color="#4A90E2" />
        <p>Generating blueprint... please wait.</p>
    </div>
);