using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace BackEnd.Entidades
{
    public class Actor
    {
        public int Id { get; set; }
        [Required]
        [StringLength(maximumLength: 200)]
        
        public string Nombre { get; set; }
        public string Biografia { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public string Foto { get; set; }

       //Si yo tengo un actor, traigo en todas las peliculas en las que participa
        public List<PeliculasActores> PeliculasActores { get; set; }
    }
}