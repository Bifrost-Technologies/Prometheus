
const API_BASE = "http://localhost:5283";

export interface BlueprintResponse {
  jobId: string;
}

export interface StatusResponse {
  status: 'Pending' | 'Complete';
}

export async function postWithTimeout<T>(
    url: string,
    data: unknown,
    timeout = 5000
): Promise<T> {
    const controller = new AbortController();
    const id = setTimeout(() => controller.abort(), timeout);

    try {
        const res = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data),
            signal: controller.signal
        });

        if (!res.ok) {
            const errText = await res.text();
            throw new Error(`Status ${res.status}: ${errText}`);
        }

        return await res.json();
    } finally {
        clearTimeout(id);
    }
}

export async function requestBlueprint(prompt: string) {
    return await postWithTimeout<{ jobId: string }>(
        `${API_BASE}/requestblueprint`,
        { prompt }
    );
}

export async function checkStatus(jobId: string) {
    const result = await postWithTimeout<{ status: 'Pending' | 'Complete' }>(
        `${API_BASE}/checkstatus`,
        { jobId }
    );
    return result.status;
}

export async function getBlueprint(jobId: string) {
    const result = await postWithTimeout<{ blueprint: string }>(
        `${API_BASE}/getblueprint`,
        { jobId }
    );
    return result.blueprint;
}
