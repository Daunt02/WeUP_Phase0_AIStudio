import { GoogleGenAI, Type } from "@google/genai";
import { WLLSConfig, MapMarkerPayload, EventCardUI, NightlifeItem } from '@/types/index';

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

class UIService {
  /**
   * Maps a save action to WLLS interaction parameters (GAI-WLLS-P02).
   */
  async getSaveInteraction(): Promise<WLLSConfig> {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-2.0-flash",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-WLLS-P02\n\nSYSTEM\nMap save action to WLLS.\n\nUSER\nGenerate WLLS output for a successful event save.` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            color: { type: Type.STRING },
            motion: { type: Type.STRING },
            intensity: { type: Type.STRING },
            rhythm: { type: Type.STRING }
          },
          required: ["color", "motion", "intensity", "rhythm"]
        }
      }
    });

    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse save interaction JSON:', e);
      return { color: '#00FF9C', motion: 'static', intensity: 'low', rhythm: 'none' };
    }
  }

  /**
   * Prepares a map marker payload (GAI-UI-P01).
   */
  async prepareMapMarker(event: NightlifeItem): Promise<MapMarkerPayload> {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-2.0-flash",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-UI-P01\n\nSYSTEM\nPrepare map marker payload.\n\nUSER\nGenerate map marker data for:\n\n${JSON.stringify(event)}` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            label: { type: Type.STRING },
            district: { type: Type.STRING },
            state: { 
              type: Type.STRING,
              enum: ["upcoming", "live", "ended"]
            },
            confidence: { type: Type.NUMBER },
            wlls: {
              type: Type.OBJECT,
              properties: {
                color: { type: Type.STRING },
                motion: { type: Type.STRING },
                intensity: { type: Type.STRING },
                rhythm: { type: Type.STRING }
              },
              required: ["color", "motion", "intensity", "rhythm"]
            }
          },
          required: ["label", "district", "state", "confidence", "wlls"]
        }
      }
    });

    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse map marker JSON:', e);
      return { 
        label: event.title, 
        district: event.neighborhood || 'HOUSTON', 
        state: 'upcoming', 
        confidence: 0.5, 
        wlls: { color: '#ffffff', motion: 'none', intensity: 'low', rhythm: 'none' } 
      };
    }
  }

  /**
   * Prepares event card UI data (GAI-UI-P02).
   */
  async prepareEventCard(event: NightlifeItem): Promise<EventCardUI> {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-2.0-flash",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-UI-P02\n\nSYSTEM\nPrepare event card UI data.\n\nUSER\nGenerate event card UI for:\n\n${JSON.stringify(event)}` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            title: { type: Type.STRING },
            time: { type: Type.STRING },
            location: { type: Type.STRING },
            badges: { type: Type.ARRAY, items: { type: Type.STRING } }
          },
          required: ["title", "time", "location", "badges"]
        }
      }
    });

    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse event card JSON:', e);
      return { title: event.title, time: 'TBD', location: event.venue_name, badges: [] };
    }
  }
}

export const uiService = new UIService();
