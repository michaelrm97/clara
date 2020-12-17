#ifndef API_H_
#define API_H_

typedef enum apiStatus {
  NoChange,
  NewConfig,
  Stopped
} ApiStatus;

void setupApi();

// Get the current config (/api/current)
ApiStatus getConfig();
// Move onto next config (/api/next)
ApiStatus nextConfig();

// Check if there is a new config (non-blocking)
ApiStatus checkCurrent();

// Load the currently set config
// Returns true if successful
// On failure- resets current config
bool loadConfig();

#endif /* API_H_ */
