import { useEffect, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { rutaInicio, useAuth } from '../auth/AuthContext';
import { usePermisos } from '../auth/permisos';
import { iniciales, turnoActivo } from '../utils/formato';
import BuscadorGlobal from './BuscadorGlobal';
import Icono from './Icono';
import './Layout.css';

/** Contadores de la barra lateral: turnos de hoy, tareas abiertas y trabajos en laboratorio. */
function useContadores() {
  const { pathname } = useLocation();
  const [c, setC] = useState({});

  useEffect(() => {
    let vigente = true;
    const cargar = () =>
      Promise.all([api('/turnos'), api('/tareas'), api('/laboratorio?estado=Enviado')])
        .then(([turnos, tareas, lab]) => {
          if (vigente) setC({ agenda: turnos.filter(turnoActivo).length, tareas: tareas.length, laboratorio: lab.length });
        })
        .catch(() => {});
    cargar();
    const intervalo = setInterval(cargar, 60_000);
    return () => {
      vigente = false;
      clearInterval(intervalo);
    };
  }, [pathname]);

  return c;
}

export default function Layout() {
  const { usuario, esOdontologo, logout } = useAuth();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const [menuAbierto, setMenuAbierto] = useState(false);
  const contadores = useContadores();
  const { puede } = usePermisos();
  const verClinica = esOdontologo || puede('HistoriaClinicaOdontograma', 'VerResumenes');
  const verFinanzas = esOdontologo || puede('FinanzasGenerales', 'VerResumenes');

  useEffect(() => setMenuAbierto(false), [pathname]);

  const secciones = [
    {
      titulo: 'Principal',
      links: [
        { to: rutaInicio(usuario.rol), texto: 'Inicio', icono: 'inicio' },
        { to: '/agenda', texto: 'Agenda', icono: 'agenda', badge: contadores.agenda },
        { to: '/turnos', texto: 'Turnos y tareas', icono: 'tareas', badge: contadores.tareas },
        { to: '/pacientes', texto: 'Pacientes', icono: 'pacientes' },
      ],
    },
    {
      titulo: 'Clínica',
      links: [
        ...(verClinica
          ? [
              { to: '/clinica/historia', texto: 'Historia clínica', icono: 'historia' },
              { to: '/clinica/odontograma', texto: 'Odontograma', icono: 'diente' },
            ]
          : []),
        { to: '/laboratorio', texto: 'Laboratorio dental', icono: 'laboratorio', badge: contadores.laboratorio },
      ],
    },
    {
      titulo: 'Gestión',
      links: [
        { to: '/pagos', texto: 'Pagos de pacientes', icono: 'pagos' },
        ...(verFinanzas ? [{ to: '/caja', texto: 'Finanzas', icono: 'finanzas' }] : []),
      ],
    },
    {
      titulo: 'Cuenta',
      links: [{ to: '/perfil', texto: 'Perfil y configuración', icono: 'ajustes' }],
    },
  ];

  return (
    <div className={`layout ${menuAbierto ? 'layout--menu' : ''}`}>
      <aside className="layout__lateral">
        <div className="marca">
          <span className="marca__logo"><Icono nombre="diente" tamano={20} /></span>
          <span>
            <b>Odontia</b>
            <small>Gestión odontológica</small>
          </span>
        </div>

        <nav className="menu" aria-label="Principal">
          {secciones.map((s) => (
            <div key={s.titulo} className="menu__seccion">
              <span className="menu__titulo">{s.titulo}</span>
              {s.links.map((l) => (
                <NavLink key={l.to} to={l.to} end={l.to !== '/pacientes'} className="menu__link">
                  <Icono nombre={l.icono} />
                  <span>{l.texto}</span>
                  {l.badge > 0 && <span className="menu__badge">{l.badge}</span>}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>

        <div className="usuario">
          <span className="avatar">{iniciales(usuario.nombre)}</span>
          <span className="usuario__datos">
            <b>{usuario.nombre}</b>
            <small>{esOdontologo ? 'Odontólogo/a · Admin' : 'Secretaria · Recepción'}</small>
          </span>
          <button
            type="button"
            className="fantasma chico"
            title="Cerrar sesión"
            aria-label="Cerrar sesión"
            onClick={() => {
              logout();
              navigate('/login');
            }}
          >
            <Icono nombre="salir" />
          </button>
        </div>
      </aside>

      <div className="layout__principal">
        <header className="layout__barra">
          <button type="button" className="fantasma layout__hamburguesa" aria-label="Menú" onClick={() => setMenuAbierto((v) => !v)}>
            <Icono nombre="menu" />
          </button>
          <BuscadorGlobal />
          <button type="button" className="fantasma layout__campana" aria-label="Notificaciones" title="Notificaciones">
            <Icono nombre="campana" />
          </button>
        </header>
        <main className="layout__contenido">
          <Outlet />
        </main>
      </div>
      {menuAbierto && <div className="layout__velo" onClick={() => setMenuAbierto(false)} />}
    </div>
  );
}
