export function getBackendOrigin(): string {
    const w = globalThis as unknown as { __BACKEND_ORIGIN__?: string };
    if (w && typeof w.__BACKEND_ORIGIN__ === "string" && w.__BACKEND_ORIGIN__) {
        return w.__BACKEND_ORIGIN__;
    }
    // Default to ASP.NET dev https port configured for CORS in vite.config
    return "https://localhost:7039";
}

export async function fetchJson<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
    const origin = getBackendOrigin();
    const url = path.startsWith("http") ? path : `${origin}${path}`;
    const response = await fetch(url, {
        headers: {
            "Accept": "application/json",
            "Content-Type": "application/json",
            ...(init && init.headers ? init.headers : {})
        },
        ...init
    });

    if (!response.ok) {
        const text = await safeReadText(response);
        throw new Error(`HTTP ${response.status} ${response.statusText}${text ? ` - ${text}` : ""}`);
    }

    return (await response.json()) as TResponse;
}

async function safeReadText(response: Response): Promise<string> {
    try {
        return await response.text();
    } catch {
        return "";
    }
}


