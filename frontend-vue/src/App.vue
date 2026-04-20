<template>
  <q-layout view="hHh lpR fFf" class="app-layout">
    <q-header bordered class="header">
      <q-toolbar>
        <q-toolbar-title>WeUP Discovery Map</q-toolbar-title>
      </q-toolbar>
    </q-header>

    <q-page-container>
      <q-page class="map-page">
        <MapSurface @event-selected="onEventSelected" />
      </q-page>
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import MapSurface from "./components/MapSurface.vue";

function onEventSelected(eventId: string): void {
  console.info("Selected canonical eventId", eventId);
}
</script>

<style scoped>
.app-layout {
  min-height: 100vh;
  background: linear-gradient(180deg, #f7fbff 0%, #eef5ff 100%);
}

.header {
  background: #ffffff;
  color: #1f2937;
}

.map-page {
  height: calc(100vh - 50px);
  padding: 12px;
}

@media (max-width: 768px) {
  .map-page {
    padding: 8px;
    height: calc(100vh - 50px);
  }
}
</style>
