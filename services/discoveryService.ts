import { GoogleGenAI } from "@google/genai";
import { NightlifeItem } from '@/types';

// Helper to get Gemini AI client
const getAI = () => {
  const apiKey = process.env.GEMINI_API_KEY || process.env.NEXT_PUBLIC_GEMINI_API_KEY;
  if (!apiKey) {
    throw new Error('GEMINI_API_KEY or NEXT_PUBLIC_GEMINI_API_KEY is not defined');
  }
  return new GoogleGenAI({ apiKey });
};

export const discoveryService = {
  /**
   * Generates localized search terms for finding flyers or social proof of an event.
   */
  generateFlyerSearchTerms: async (event: Partial<NightlifeItem>): Promise<string[]> => {
    try {
      const ai = getAI();
      const prompt = `
        Given the following nightlife event details, generate 3 specific, targeted search terms 
        that a user would use on Instagram or Google Images to find a flyer, ticket link, 
        or photographic evidence of this event. 
        
        Event: ${event.title}
        Venue: ${event.venue_name}
        Address: ${event.address}
        Date: ${event.start_time}
        
        Return ONLY a JSON array of strings. No markdown, no explanation.
        Example output: ["Venue Name Event Name Instagram", "Event Name Houston Flyer", "Venue Name Schedule April 2026"]
      `;

      const response = await ai.models.generateContent({
        model: "gemini-3-flash-preview",
        contents: [{ parts: [{ text: prompt }] }]
      });
      
      const text = response.text || '[]';
      
      // Clean up potential markdown formatting
      const cleaned = text.replace(/```json/g, '').replace(/```/g, '').trim();
      return JSON.parse(cleaned);
    } catch (err) {
      console.error('Failed to generate search terms via Gemini:', err);
      // Fallback search terms
      return [
        `${event.title} at ${event.venue_name} flyer`,
        `${event.venue_name} ${new Date(event.start_time!).toLocaleDateString()} event`,
        `${event.title} ${event.address} instagram`
      ];
    }
  },

  /**
   * Generates a "Seed" of events for a new area using AI discovery.
   */
  generateSeedEvents: async (location: string): Promise<Partial<NightlifeItem>[]> => {
    try {
      const ai = getAI();
      const prompt = `
        Discover 5 real or highly probable upcoming nightlife/tech/culture events in ${location}.
        Format the output as a JSON array of NightlifeItem objects.
        Required fields: id, title, description, venue_name, address, latitude, longitude, start_time, end_time, category.
        
        Return ONLY JSON.
      `;

      const response = await ai.models.generateContent({
        model: "gemini-3-flash-preview",
        contents: [{ parts: [{ text: prompt }] }]
      });

      const text = response.text || '[]';
      const cleaned = text.replace(/```json/g, '').replace(/```/g, '').trim();
      return JSON.parse(cleaned);
    } catch (err) {
      console.error('Failed to generate seed events:', err);
      return [];
    }
  }
};
