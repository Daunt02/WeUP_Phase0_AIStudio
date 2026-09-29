import { GoogleGenAI, Type } from "@google/genai";
import { ProfileState, SaveActionResponse } from '@/types/index';

let aiInstance: GoogleGenAI | null = null;

function getAI() {
  if (!aiInstance) {
    const apiKey = process.env.NEXT_PUBLIC_GEMINI_API_KEY;
    if (!apiKey) {
      throw new Error('NEXT_PUBLIC_GEMINI_API_KEY is missing. Please add it to your environment variables.');
    }
    aiInstance = new GoogleGenAI({ apiKey });
  }
  return aiInstance;
}

class ProfileService {
  /**
   * Returns a snapshot of the user's profile context (GAI-PROF-P01).
   */
  async getProfileSnapshot(): Promise<ProfileState> {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-2.0-flash",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-PROF-P01\n\nSYSTEM\nReturn profile snapshot.\n\nUSER\nProvide current profile state.` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            city: { type: Type.STRING },
            saved_count: { type: Type.NUMBER },
            folders: {
              type: Type.ARRAY,
              items: {
                type: Type.OBJECT,
                properties: {
                  name: { type: Type.STRING },
                  count: { type: Type.NUMBER }
                },
                required: ["name", "count"]
              }
            }
          },
          required: ["city", "saved_count", "folders"]
        }
      }
    });

    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse profile snapshot JSON:', e);
      return { city: 'Houston', saved_count: 0, folders: [] };
    }
  }

  /**
   * Saves an event to the user's profile folders (GAI-PROF-P02).
   */
  async saveEvent(eventId: string): Promise<SaveActionResponse> {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-2.0-flash",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-PROF-P02\n\nSYSTEM\nSave event state.\n\nUSER\nSave event with ID: ${eventId}` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            event_id: { type: Type.STRING },
            folder: { type: Type.STRING },
            state: { 
              type: Type.STRING,
              enum: ["saved", "unsaved"]
            },
            timestamp: { type: Type.STRING }
          },
          required: ["event_id", "folder", "state", "timestamp"]
        }
      }
    });

    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse save event JSON:', e);
      return { event_id: eventId, folder: 'General', state: 'saved', timestamp: new Date().toISOString() };
    }
  }
}

export const profileService = new ProfileService();
