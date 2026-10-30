import { Navigate, Route, Routes } from 'react-router-dom';
import { ROLES, RutaProtegida, rutaInicio, useAuth } from './auth/AuthContext';
import Layout from './components/Layout';
import Agenda from './pages/Agenda';
import Caja from './pages/Caja';
import Clinica from './pages/Clinica';
import DashboardOdontologo from './pages/DashboardOdontologo';
import DashboardSecretaria from './pages/DashboardSecretaria';
import FichaPaciente from './pages/FichaPaciente';
import Laboratorio from './pages/Laboratorio';
import AceptarInvitacion from './pages/AceptarInvitacion';
import Login from './pages/Login';
import RegistroDueno from './pages/RegistroDueno';
import NuevoPaciente from './pages/NuevoPaciente';
import Pacientes from './pages/Pacientes';
import Pagos from './pages/Pagos';
import Perfil from './pages/Perfil';
import TurnosYTareas from './pages/TurnosYTareas';

const { SECRETARIA, ODONTOLOGO } = ROLES;
const TODOS = [SECRETARIA, ODONTOLOGO];

export default function App() {
  const { usuario } = useAuth();

  return (
    <Routes>
      <Route path="/login" element={usuario ? <Navigate to={rutaInicio(usuario.rol)} replace /> : <Login />} />
      <Route path="/registro" element={usuario ? <Navigate to={rutaInicio(usuario.rol)} replace /> : <RegistroDueno />} />
      <Route path="/invitacion/:token" element={usuario ? <Navigate to={rutaInicio(usuario.rol)} replace /> : <AceptarInvitacion />} />

      <Route element={<RutaProtegida roles={TODOS}><Layout /></RutaProtegida>}>
        <Route path="/secretaria" element={<RutaProtegida roles={[SECRETARIA]}><DashboardSecretaria /></RutaProtegida>} />
        <Route path="/odontologo" element={<RutaProtegida roles={[ODONTOLOGO]}><DashboardOdontologo /></RutaProtegida>} />
        <Route path="/agenda" element={<Agenda />} />
        <Route path="/turnos" element={<TurnosYTareas />} />
        <Route path="/pacientes" element={<Pacientes />} />
        <Route path="/pacientes/nuevo" element={<NuevoPaciente />} />
        <Route path="/pacientes/:id" element={<FichaPaciente />} />
        <Route path="/clinica/:vista" element={<RutaProtegida roles={TODOS}><Clinica /></RutaProtegida>} />
        <Route path="/laboratorio" element={<Laboratorio />} />
        <Route path="/pagos" element={<Pagos />} />
        <Route path="/caja" element={<RutaProtegida roles={TODOS}><Caja /></RutaProtegida>} />
        <Route path="/perfil" element={<Perfil />} />
      </Route>

      <Route path="*" element={<Navigate to={usuario ? rutaInicio(usuario.rol) : '/login'} replace />} />
    </Routes>
  );
}
