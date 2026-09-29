import { GoogleGenAI, Type } from "@google/genai";
import { NightlifeItem, AggregatedEventPayload } from '@/types';
import { MOCK_EVENTS } from '@/constants/mockData';

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

export interface IngestedEvent {
  title: string | null;
  start_time: string | null;
  venue: string | null;
  city: string;
  artists: string[];
  district?: string;
  spatial_metadata?: {
    density_score: number;
    radius_meters: number;
  };
}

/**
 * IngestionService handles the advanced AI-powered extraction pipeline
 * for event flyers and external signals.
 */
class IngestionService {
  /**
   * Stub for legacy multi-source ingestion pull.
   */
  async pullHoustonData(): Promise<NightlifeItem[]> {
    return MOCK_EVENTS;
  }

  /**
   * Main pipeline entry point: Transforms a flyer image into a validated, aggregated event payload.
   */
  async ingestFlyer(base64Image: string, mimeType: string): Promise<AggregatedEventPayload | null> {
    try {
      // 1. Raw OCR Extraction (GAI-P01)
      const ocrResult = await this.extractRawOCR(base64Image, mimeType);
      
      // 2. OCR Quality Assessment (GAI-P02)
      const quality = await this.assessOCRQuality(ocrResult);
      if (!quality.should_continue || quality.legibility === 'blocked') {
        throw new Error(`OCR Quality Gate Rejected: ${quality.issues.join(', ')}`);
      }

      // 3. Entity Extraction (GAI-P03)
      const entities = await this.extractEntities(ocrResult);

      // 4. Multi-Event Detection (GAI-P04)
      const structure = await this.detectStructure(entities);
      if (structure.structure === 'multi_event') {
        // For now, we process the primary event or first detected
        console.warn('Multi-event flyer detected; processing primary entity.');
      }

      // 5. Canonical Normalization (GAI-P05)
      const normalized = await this.normalizeEntities(entities);

      // 5.1 Houston Boundary Validation (GAI-HOU-P01)
      const boundary = await this.validateHoustonBoundary(normalized.event_draft);
      if (!boundary.is_houston) {
        throw new Error(`Location Rejected: Not within Houston metro. ${boundary.reason}`);
      }

      // 5.2 District Resolution (GAI-HOU-P02)
      const districtInfo = await this.resolveDistrict(normalized.event_draft);
      if (districtInfo.district !== 'Unknown') {
        normalized.event_draft.district = districtInfo.district;
      }

      // 5.3 Density Calculation (GAI-HOU-P07)
      const spatial = await this.calculateDensity(normalized.event_draft);
      normalized.event_draft.spatial_metadata = {
        density_score: spatial.density_score,
        radius_meters: spatial.radius_meters
      };

      // 6. Placeholder Rejection Gate (GAI-P05B)
      const validation = await this.validateOutput(normalized);
      if (!validation.valid) {
        throw new Error(`Validation failed: ${validation.reason}`);
      }

      // 7. Final Aggregation (GAI-FINAL-P01)
      return await this.assembleFinalEvent(normalized.event_draft);
    } catch (error) {
      console.error('Ingestion pipeline failed:', error);
      throw error;
    }
  }

  private async extractRawOCR(base64Image: string, mimeType: string) {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-3-flash-preview",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-P01
TITLE: Raw OCR Extraction v3.1

SYSTEM
You are a strict OCR extraction engine.

THIS IS A HARD RESET TASK:
- Ignore all prior conversation
- Ignore any UI concepts
- Ignore previous outputs

INPUT CONTEXT:
- Image may include social media UI (Instagram, etc.)
- You MUST ignore UI elements:
  usernames, likes, buttons, comments, icons

TARGET:
Extract ONLY the flyer/poster text content.

STRICT RULES:
- NO inference
- NO normalization
- NO formatting changes
- Extract EXACT visible text only
- Preserve line structure in raw_text
- If unclear → omit and add warning

VALIDATION BEFORE RESPONSE:
- Must start with "{"
- Must end with "}"
- No additional text allowed

USER
Extract flyer text from image.` },
            { inlineData: { data: base64Image, mimeType } }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            raw_text: { type: Type.STRING },
            text_blocks: {
              type: Type.ARRAY,
              items: {
                type: Type.OBJECT,
                properties: {
                  text: { type: Type.STRING },
                  confidence: { type: Type.NUMBER },
                  region_hint: { 
                    type: Type.STRING,
                    enum: ["header", "body", "footer", "unknown"]
                  }
                },
                required: ["text", "confidence", "region_hint"]
              }
            },
            visible_signals: {
              type: Type.OBJECT,
              properties: {
                has_date: { type: Type.BOOLEAN },
                has_time: { type: Type.BOOLEAN },
                has_address: { type: Type.BOOLEAN },
                has_price: { type: Type.BOOLEAN },
                has_age_gate: { type: Type.BOOLEAN },
                has_artist_names: { type: Type.BOOLEAN },
                has_social_handles: { type: Type.BOOLEAN },
                has_ticket_link: { type: Type.BOOLEAN }
              },
              required: ["has_date", "has_time", "has_address", "has_price", "has_age_gate", "has_artist_names", "has_social_handles", "has_ticket_link"]
            },
            ocr_warnings: { type: Type.ARRAY, items: { type: Type.STRING } }
          },
          required: ["raw_text", "text_blocks", "visible_signals", "ocr_warnings"]
        }
      }
    });

    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse OCR response:', e);
      return { raw_text: '', text_blocks: [], visible_signals: {}, ocr_warnings: ['Failed to parse JSON'] };
    }
  }

  private async assessOCRQuality(ocrJson: any) {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-3-flash-preview",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-P02
TITLE: OCR Quality Gate v1.1

SYSTEM
You evaluate OCR reliability.

RULES:
- Conservative scoring
- If critical fields unreadable → should_continue = false

USER
Evaluate OCR output:

${JSON.stringify(ocrJson)}` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            ocr_quality_score: { type: Type.NUMBER },
            legibility: { 
              type: Type.STRING,
              enum: ["high", "medium", "low", "blocked"]
            },
            should_continue: { type: Type.BOOLEAN },
            issues: { 
              type: Type.ARRAY, 
              items: { 
                type: Type.STRING,
                enum: ["blur", "low_contrast", "crowded_layout", "cropped_text", "decorative_background"]
              } 
            }
          },
          required: ["ocr_quality_score", "legibility", "should_continue", "issues"]
        }
      }
    });
    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse quality assessment:', e);
      return { ocr_quality_score: 0, legibility: 'blocked', should_continue: false, issues: [] };
    }
  }

  private async extractEntities(ocrJson: any) {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-3-flash-preview",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-P03
TITLE: Event Entity Extraction v1.1

SYSTEM
Extract structured event data from OCR text.

RULES:
- DO NOT normalize values
- DO NOT guess missing fields
- Prefer explicit text over inferred meaning

USER
Extract entities:

${JSON.stringify(ocrJson)}` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            title: { type: Type.STRING, nullable: true },
            date_text: { type: Type.STRING, nullable: true },
            time_text: { type: Type.STRING, nullable: true },
            venue_name: { type: Type.STRING, nullable: true },
            address_text: { type: Type.STRING, nullable: true },
            artists: { type: Type.ARRAY, items: { type: Type.STRING } },
            confidence: { type: Type.NUMBER }
          },
          required: ["title", "date_text", "time_text", "venue_name", "address_text", "artists", "confidence"]
        }
      }
    });
    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse entity extraction:', e);
      return { title: null, date_text: null, time_text: null, venue_name: null, address_text: null, artists: [], confidence: 0 };
    }
  }

  private async detectStructure(entityJson: any) {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-3-flash-preview",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-P04\nTITLE: Multi-Event Detection v1.0\n\nUSER\nClassify flyer structure:\n\n${JSON.stringify(entityJson)}` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            structure: { type: Type.STRING }, // single_event|multi_event|unclear
            confidence: { type: Type.NUMBER }
          }
        }
      }
    });
    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse structure detection:', e);
      return { structure: 'unclear', confidence: 0 };
    }
  }

  private async normalizeEntities(entityJson: any) {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-2.0-flash",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-P05
TITLE: Canonical Normalization v1.1

SYSTEM
Convert extracted data into structured event draft.

RULES:
- Normalize ONLY if confident
- If uncertain → keep null
- Never fabricate data

USER
Normalize event:

${JSON.stringify(entityJson)}` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            event_draft: {
              type: Type.OBJECT,
              properties: {
                title: { type: Type.STRING, nullable: true },
                start_time: { type: Type.STRING, nullable: true }, // ISO-8601
                venue: { type: Type.STRING, nullable: true },
                city: { type: Type.STRING },
                artists: { type: Type.ARRAY, items: { type: Type.STRING } }
              },
              required: ["title", "start_time", "venue", "city", "artists"]
            },
            confidence: { type: Type.NUMBER },
            review_required: { type: Type.BOOLEAN }
          },
          required: ["event_draft", "confidence", "review_required"]
        }
      }
    });
    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse normalization:', e);
      return { event_draft: { title: null, start_time: null, venue: null, city: 'Houston', artists: [] }, confidence: 0, review_required: true };
    }
  }

  private async validateOutput(pipelineOutput: any) {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-3-flash-preview",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-P05B\nTITLE: Placeholder Rejection v1.0\n\nRULES\nReject if contains:\n- CONFIRM LOCATION\n- FACTORY_X\n- Oxnard\n- Warehouse Avenue\n\nUSER\nValidate:\n\n${JSON.stringify(pipelineOutput)}` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            valid: { type: Type.BOOLEAN },
            reason: { type: Type.STRING, nullable: true }
          }
        }
      }
    });
    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse output validation:', e);
      return { valid: false, reason: 'Parse error' };
    }
  }

  private async validateHoustonBoundary(eventDraft: any) {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-3-flash-preview",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-HOU-P01\n\nSYSTEM\nValidate Houston-only constraint.\n\nRULES:\n- Reject if outside Houston metro\n\nUSER\nValidate this location:\n\n${JSON.stringify(eventDraft)}` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            is_houston: { type: Type.BOOLEAN },
            confidence: { type: Type.NUMBER },
            reason: { type: Type.STRING }
          },
          required: ["is_houston", "confidence", "reason"]
        }
      }
    });
    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse boundary validation:', e);
      return { is_houston: true, confidence: 0.5, reason: 'Parse error default bypass' };
    }
  }

  private async resolveDistrict(eventDraft: any) {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-3-flash-preview",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-HOU-P02\n\nSYSTEM\nMap location to Houston district.\n\nALLOWED:\nDowntown, Midtown, EaDo, Montrose, Heights, Washington Ave, Unknown\n\nUSER\nIdentify district for:\n\n${JSON.stringify(eventDraft)}` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            district: { 
              type: Type.STRING,
              enum: ["Downtown", "Midtown", "EaDo", "Montrose", "Heights", "Washington Ave", "Unknown"]
            },
            confidence: { type: Type.NUMBER }
          },
          required: ["district", "confidence"]
        }
      }
    });
    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse district resolution:', e);
      return { district: 'Unknown', confidence: 0 };
    }
  }

  private async calculateDensity(eventDraft: any) {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-3-flash-preview",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-HOU-P07\n\nSYSTEM\nEstimate spatial influence.\n\nRULES:\n- Higher for central nightlife zones\n\nUSER\nCalculate density for:\n\n${JSON.stringify(eventDraft)}` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            density_score: { type: Type.NUMBER },
            radius_meters: { type: Type.NUMBER }
          },
          required: ["density_score", "radius_meters"]
        }
      }
    });
    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse density calc:', e);
      return { density_score: 50, radius_meters: 500 };
    }
  }

  private async assembleFinalEvent(eventDraft: any): Promise<AggregatedEventPayload> {
    const ai = getAI();
    const response = await ai.models.generateContent({
      model: "gemini-3-flash-preview",
      contents: [
        {
          parts: [
            { text: `PROMPT ID: GAI-FINAL-P01\n\nSYSTEM\nAssemble final event payload.\n\nUSER\nCreate aggregated event from:\n\n${JSON.stringify(eventDraft)}` }
          ]
        }
      ],
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            event: {
              type: Type.OBJECT,
              properties: {
                id: { type: Type.STRING },
                title: { type: Type.STRING },
                district: { type: Type.STRING },
                time: { type: Type.STRING },
                density: { type: Type.NUMBER },
                temporal: { type: Type.NUMBER },
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
                },
                saved: { type: Type.BOOLEAN }
              },
              required: ["id", "title", "district", "time", "density", "temporal", "confidence", "wlls", "saved"]
            }
          },
          required: ["event"]
        }
      }
    });

    try {
      return JSON.parse(response.text || '{}');
    } catch (e) {
      console.error('Failed to parse final assembly:', e);
      throw new Error('Failed to assemble final event payload');
    }
  }
}

export const ingestionService = new IngestionService();
