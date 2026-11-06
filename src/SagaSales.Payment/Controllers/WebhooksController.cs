// RETIRED — this controller accepted provider callbacks without verifying their signature, which the
// file itself warned about ("must not be exposed publicly"). Online payments are E-04b (v1) and the
// MVP records manual charges only, so the endpoint is removed until there is a real gateway with
// signed webhooks.
//
// Kept as a tombstone so nobody re-exposes an unauthenticated endpoint by accident.
