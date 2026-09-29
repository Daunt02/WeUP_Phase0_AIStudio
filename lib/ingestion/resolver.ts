import { IngestionSourceKind } from '@/types/ingestion';
import { IEventSourceAdapter, IEventSourceAdapterResolver } from './interfaces';
import { ManualAdapter } from './adapters/ManualAdapter';
import { UrlAdapter } from './adapters/UrlAdapter';
import { VenuePageAdapter } from './adapters/VenuePageAdapter';
import { FlyerOcrAdapter } from './adapters/FlyerOcrAdapter';

export class EventSourceAdapterResolver implements IEventSourceAdapterResolver {
  private adapters: Map<IngestionSourceKind, IEventSourceAdapter> = new Map();

  constructor() {
    this.register(new ManualAdapter());
    this.register(new UrlAdapter());
    this.register(new VenuePageAdapter());
    this.register(new FlyerOcrAdapter());
  }

  private register(adapter: IEventSourceAdapter) {
    const caps = adapter.getCapabilities();
    caps.supportedKinds.forEach(kind => {
      this.adapters.set(kind, adapter);
    });
  }

  resolve(kind: IngestionSourceKind): IEventSourceAdapter {
    const adapter = this.adapters.get(kind);
    if (!adapter) {
      throw new Error(`No adapter found for source kind: ${kind}`);
    }
    return adapter;
  }
}

export const adapterResolver = new EventSourceAdapterResolver();
