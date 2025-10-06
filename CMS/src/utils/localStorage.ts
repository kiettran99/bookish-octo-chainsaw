export function readFromStorage<T>(key: string): T | null {
  if (typeof window === 'undefined') {
    return null
  }

  try {
    const raw = window.localStorage.getItem(key)
    if (!raw) {
      return null
    }

    return JSON.parse(raw) as T
  } catch (error) {
    console.warn(`Failed to parse storage key "${key}"`, error)
    return null
  }
}

export function writeToStorage<T>(key: string, value: T | null | undefined): void {
  if (typeof window === 'undefined') {
    return
  }

  if (value === undefined || value === null) {
    window.localStorage.removeItem(key)
    return
  }

  window.localStorage.setItem(key, JSON.stringify(value))
}
