export function getAuthHeader(): Record<string, string> {
  if (typeof window === 'undefined') return {};
  try {
    const token = localStorage.getItem('weup_dev_token');
    if (token) return { Authorization: `Bearer ${token}` };
  } catch (e) {
    // ignore
  }
  return {};
}

export function setDevToken(token: string) {
  if (typeof window === 'undefined') return;
  try { localStorage.setItem('weup_dev_token', token); } catch (e) { }
}
