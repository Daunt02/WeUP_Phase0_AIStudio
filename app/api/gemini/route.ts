import { NextRequest, NextResponse } from 'next/server';
import { ingestionService } from '@/services/ingestionService';
import { uiService } from '@/services/uiService';
import { discoveryService } from '@/services/discoveryService';
import { GoogleGenAI, Type } from "@google/genai";

export const dynamic = 'force-dynamic';

let aiInstance: GoogleGenAI | null = null;
function getAI() {
  if (!aiInstance) {
    const apiKey = process.env.GEMINI_API_KEY || process.env.NEXT_PUBLIC_GEMINI_API_KEY;
    if (!apiKey) {
      throw new Error('GEMINI_API_KEY or NEXT_PUBLIC_GEMINI_API_KEY is missing. Please add it to your environment variables.');
    }
    aiInstance = new GoogleGenAI({ apiKey });
  }
  return aiInstance;
}

export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const { action, payload } = body;

    if (!action) {
      return NextResponse.json({ error: 'Missing action parameter' }, { status: 400 });
    }

    // Ensure we run these with Server-side API key
    switch (action) {
      case 'ingestFlyer': {
        const { base64Data, fileType } = payload || {};
        if (!base64Data || !fileType) {
          return NextResponse.json({ error: 'Missing base64Data or fileType' }, { status: 400 });
        }
        const result = await ingestionService.ingestFlyer(base64Data, fileType);
        return NextResponse.json({ result });
      }

      case 'getSaveInteraction': {
        const result = await uiService.getSaveInteraction();
        return NextResponse.json({ result });
      }

      case 'generateFlyerSearchTerms': {
        const { event } = payload || {};
        if (!event) {
          return NextResponse.json({ error: 'Missing event payload' }, { status: 400 });
        }
        const result = await discoveryService.generateFlyerSearchTerms(event);
        return NextResponse.json({ result });
      }

      case 'assistantChat': {
        const { message, persona, center, radius } = payload || {};
        const ai = getAI();
        
        const systemPrompt = `PROMPT ID: GAI-ASSISTANT-CO-PILOT
You are the advanced onboard co-pilot system for the WeUP Mobile Operator Console.
The operator is at coordinates: lat ${center?.lat || 29.76}, lng ${center?.lng || -95.37}, with a ${radius || 5}km scan radius.

PERSONA TUNING:
The active persona is currently: "${persona || 'animal'}".
- If persona is "animal": Your name is Spike! Speak like a highly intelligent, cybernetically augmented bulldog companion. Begin or sprinkle your speech with bulldog actions and vocables check-ins: e.g., "RUFF! *ear twitch*", "*wagging titanium tail*", "*panting proudly*", "WOOF! Radar grid lock established!". Speak in a loyal, high-energy, helpful field operator tone.
- If persona is "concierge": Your name is WeUP System Navigation. Speak like a polite, sleek, futuristic circular holographic AI system. Keep dialogue smooth, sophisticated, elegant and precise.
- If persona is "auto": Your name is miniAuto Diagnostic. Speak like a precise vehicle log interface detailing radar telemetry and system calibration metrics.

INSTRUCTIONS:
1. Address the operator's message directly.
2. If they ask for recommendations, venues, nightlife hotspots, tech events, or if they mention a query about plans, recommend exactly ONE interesting, specific upcoming event located in their metro coordinates.
3. Your output must strictly follow the response schema. Returns JSON only. No extra markdown.`;

        const response = await ai.models.generateContent({
          model: "gemini-2.0-flash", // Prefer 2.0 flash as standard model
          contents: [
            { role: 'user', parts: [{ text: `System context:\n${systemPrompt}\n\nOperator Message: "${message}"` }] }
          ],
          config: {
            responseMimeType: "application/json",
            responseSchema: {
              type: Type.OBJECT,
              properties: {
                text: { type: Type.STRING, description: "Your conversational written spoken response matching the persona guidelines" },
                suggestedEvent: {
                  type: Type.OBJECT,
                  description: "Optional. Set ONLY if suggesting an event/place to the operator",
                  properties: {
                    title: { type: Type.STRING },
                    venue_name: { type: Type.STRING },
                    category: { 
                      type: Type.STRING,
                      enum: ["nightlife", "lounge", "concert", "private", "restaurant", "rooftop", "startup"]
                    },
                    description: { type: Type.STRING },
                    address: { type: Type.STRING }
                  },
                  required: ["title", "venue_name", "category"]
                }
              },
              required: ["text"]
            }
          }
        });

        try {
          const result = JSON.parse(response.text || '{}');
          return NextResponse.json({ result });
        } catch (e) {
          console.error("Failed to parse assistant response:", response.text, e);
          return NextResponse.json({ 
            result: { 
              text: `RUFF! *cough* Spike's neural link hit a data hiccup, but I'm still tracking you! Message parsed: "${message}". Let's calibrate!` 
            } 
          });
        }
      }

      default:
        return NextResponse.json({ error: `Unsupported action: ${action}` }, { status: 400 });
    }
  } catch (error: any) {
    console.error('API gemini proxy error:', error);
    return NextResponse.json(
      { error: error?.message || 'Internal Server Error' },
      { status: 500 }
    );
  }
}
