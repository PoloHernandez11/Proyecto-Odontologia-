import { getApp, getApps, initializeApp } from 'firebase/app';
import { getAuth, GoogleAuthProvider, signInWithPopup, signOut } from 'firebase/auth';

// Configuración pública de la app web de Firebase (no es secreta). Viene de frontend/.env.local.
const config = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY ?? '',
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN ?? '',
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID ?? '',
  appId: import.meta.env.VITE_FIREBASE_APP_ID ?? '',
};

export const firebaseConfigurado = Boolean(config.apiKey && config.projectId && config.appId);

/** Inicializa Firebase solo cuando hace falta, así la app carga aunque falte la configuración. */
function obtenerAuth() {
  if (!firebaseConfigurado) {
    throw new Error('Falta configurar Firebase en frontend/.env.local (VITE_FIREBASE_*).');
  }
  const app = getApps().length ? getApp() : initializeApp(config);
  return getAuth(app);
}

/**
 * Abre la ventana de Google, verifica a la persona con Firebase y devuelve el ID token.
 * La API lo valida y decide si la cuenta está autorizada. Firebase no guarda la sesión: la maneja la API.
 */
export async function obtenerTokenGoogle(): Promise<string> {
  const auth = obtenerAuth();
  const proveedor = new GoogleAuthProvider();
  proveedor.setCustomParameters({ prompt: 'select_account' });

  try {
    const credencial = await signInWithPopup(auth, proveedor);
    const token = await credencial.user.getIdToken();
    await signOut(auth);
    return token;
  } catch (err) {
    const codigo = (err as { code?: string }).code;
    if (codigo === 'auth/popup-closed-by-user' || codigo === 'auth/cancelled-popup-request') {
      throw new Error('Cerraste la ventana de Google antes de terminar.');
    }
    if (codigo === 'auth/popup-blocked') {
      throw new Error('El navegador bloqueó la ventana de Google. Permití ventanas emergentes para esta página.');
    }
    throw new Error('No se pudo iniciar sesión con Google. Probá de nuevo.');
  }
}
