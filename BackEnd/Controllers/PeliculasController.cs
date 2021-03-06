using AutoMapper;
using BackEnd.DTOs;
using BackEnd.Entidades;
using BackEnd.Utilidades;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BackEnd.Controllers
{   
    [ApiController]
    [Route("api/peliculas")]
    public class PeliculasController: ControllerBase
    {
        private readonly ApplicationDbContext context;
        private readonly IMapper mapper;
        private readonly IAlmacenadorArchivos almacenadorArchivos;
        private readonly string contenedor = "peliculas";

        public PeliculasController(ApplicationDbContext context, IMapper mapper, IAlmacenadorArchivos almacenadorArchivos)
        {
            this.context = context;
            this.mapper = mapper;
            this.almacenadorArchivos = almacenadorArchivos;
        }

        [HttpGet]
        public async Task<ActionResult<LandingPageDTO>> Get()
        {
            //Obtener un listado de peliculas estrenos y proximos estrenos
            var top= 6;
            var hoy= DateTime.Today;

            var proximosEstrenos = await context.Peliculas
                        .Where(x => x.FechaLanzamiento > hoy)
                        .OrderBy(x => x.FechaLanzamiento)
                        .Take(top)
                        .ToListAsync();
            
            var enCines = await context.Peliculas
                        .Where( x=> x.EnCines)
                        .OrderBy( x=> x.FechaLanzamiento)
                        .Take(top)
                        .ToListAsync();

            var resultado = new LandingPageDTO();

            resultado.ProximosEstrenos = mapper.Map<List<PeliculaDTO>>(proximosEstrenos);
            resultado.EnCines= mapper.Map<List<PeliculaDTO>>(enCines);

            return resultado;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<PeliculaDTO>> Get (int id)
        {
            var pelicula = await context.Peliculas
                    .Include(x => x.PeliculasGeneros).ThenInclude(x => x.Genero)//que incluya lo datos de peliculasgeneros 
                    .Include(x => x.PeliculasActores).ThenInclude(x => x.Actor)
                    .Include(x => x.PeliculasCines).ThenInclude(x=> x.Cine)
                    .FirstOrDefaultAsync( x => x.Id == id);
            
            if(pelicula == null) { return NotFound(); }

            var dto = mapper.Map<PeliculaDTO>(pelicula);

            dto.Actores= dto.Actores.OrderBy(x => x.Orden).ToList();

            return dto;
        } 

        //Filtrado de pelicula
        [HttpGet("filtrar")]
        public async Task<ActionResult<List<PeliculaDTO>>> Filtrar([FromQuery] PeliculasFiltradoDTO peliculasFiltradoDTO )
        {
            var peliculasQueryable = context.Peliculas.AsQueryable();
            if(!string.IsNullOrEmpty(peliculasFiltradoDTO.Titulo))
            {
                peliculasQueryable = peliculasQueryable.Where(x=> x.Titulo.Contains(peliculasFiltradoDTO.Titulo));
            }
            if(peliculasFiltradoDTO.EnCines)
            {
                peliculasQueryable = peliculasQueryable.Where (x => x.EnCines);
            }
            if(peliculasFiltradoDTO.ProximosEstrenos)
            {
                var hoy= DateTime.Today;
                peliculasQueryable= peliculasQueryable.Where(x => x.FechaLanzamiento > hoy);
            }
            if(peliculasFiltradoDTO.GeneroId != 0)
            {
                peliculasQueryable = peliculasQueryable.Where(x => x.PeliculasGeneros.Select(y => y.GeneroId)
                                                        .Contains(peliculasFiltradoDTO.GeneroId));
            }

            //Paginar
            await HttpContext.InsertarParametrosPaginacionEnCabecera(peliculasQueryable);

            var peliculas = await peliculasQueryable.Paginar(peliculasFiltradoDTO.PaginacionDTO).ToListAsync();
            return mapper.Map<List<PeliculaDTO>>(peliculas);
            
        }

        [HttpPost]
        public async Task<ActionResult<int>> Post([FromForm] PeliculaCrearDTO peliculaCreacionDTO)
        {
            var pelicula = mapper.Map<Pelicula>(peliculaCreacionDTO);

            if(peliculaCreacionDTO.Poster != null)
            {
                pelicula.Poster = await almacenadorArchivos.GuardarArchivo(contenedor, peliculaCreacionDTO.Poster);
            }

            EscribirOrdenActores(pelicula);

            context.Add(pelicula);
            await context.SaveChangesAsync();
            return pelicula.Id;
        }

        //Mostrar Cines y Generos en el Frontend
        [HttpGet("PostGet")]
        public async Task<ActionResult<PeliculasPostGetDTO>> PostGet()
        {
            var cines = await context.Cines.ToListAsync();
            var generos = await context.Genero.ToListAsync();

            var cinesDTO = mapper.Map<List<CineDTO>>(cines);
            var generosDTO = mapper.Map<List<GeneroDTO>>(generos);

            //mapeamos aqui, no automaper
            return new PeliculasPostGetDTO() {Cines = cinesDTO, Generos = generosDTO};
        }

        //Obtener los datos de la pelicula para editarla
        [HttpGet("PutGet/{id:int}")]
        public async Task<ActionResult<PeliculasPutGetDTO>> PutGet(int id)
        {   //Llamamos al metodo de Get por id
            var peliculaActionResult = await Get(id);
            if(peliculaActionResult.Result is NotFoundResult) { return NotFound();}

            var pelicula = peliculaActionResult.Value;
            //Llamamos a los generos seleccionados
            var generosSeleccionadosIds = pelicula.Generos.Select(x=> x.Id).ToList();
            //Obtenemos los generos no seleccionados
            var generosNoSeleccionados = await context.Genero
                                            .Where(x => !generosSeleccionadosIds.Contains(x.Id))
                                            .ToListAsync();

            var cinesSeleccionadosIds = pelicula.Cines.Select(x =>x.Id).ToList();
            var cinesNoSeleccionados = await context.Cines
                                            .Where(x => !cinesSeleccionadosIds.Contains(x.Id))
                                            .ToListAsync();

            var generosNoSeleccionadosDTO = mapper.Map<List<GeneroDTO>>(generosNoSeleccionados);
            var cinesNoSeleccionadosDTO = mapper.Map<List<CineDTO>>(cinesNoSeleccionados);

            var respuesta = new PeliculasPutGetDTO();
            respuesta.Pelicula = pelicula;
            respuesta.GenerosSeleccionados = pelicula.Generos;
            respuesta.GenerosNoSeleccionados = generosNoSeleccionadosDTO;
            respuesta.CinesSeleccionados = pelicula.Cines;
            respuesta.CinesNoSeleccionados = cinesNoSeleccionadosDTO;
            respuesta.Actores= pelicula.Actores;

            return respuesta;
        }

        //Metodo que realiza la actualizacion
        [HttpPut("{id:int}")]
        public async Task<ActionResult> Put(int id, [FromForm] PeliculaCrearDTO peliculaCreacionDTO)
        {
            var pelicula = await context.Peliculas
                            .Include(x => x.PeliculasActores)
                            .Include( x => x.PeliculasGeneros)
                            .Include( x=> x.PeliculasCines)
                            .FirstOrDefaultAsync( x => x.Id == id);
            
            if( pelicula == null) {return NotFound(); }

            pelicula = mapper.Map(peliculaCreacionDTO, pelicula);

            if(peliculaCreacionDTO.Poster != null)
            {
                pelicula.Poster = await almacenadorArchivos.EditarArchivo( contenedor, peliculaCreacionDTO.Poster, pelicula.Poster);
            }

            EscribirOrdenActores(pelicula);

            await context.SaveChangesAsync();
            return NoContent();
        }

        //Guardamos el orden en el que vinieron los actores
        private void EscribirOrdenActores(Pelicula pelicula)
        {
            if(pelicula.PeliculasActores != null )
            {
                for (int i = 0; i < pelicula.PeliculasActores.Count; i++)
                {
                    pelicula.PeliculasActores[i].Orden = i;
                }
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            var pelicula = await context.Peliculas.FirstOrDefaultAsync(x => x.Id == id);

            if(pelicula == null) { return NotFound();}

            context.Remove(pelicula);                                                               
            await context.SaveChangesAsync();
            //Borrar la foto
            await almacenadorArchivos.BorrarArchivo(pelicula.Poster  , contenedor);
            return NoContent();
        }

    }
}                           