namespace Walkmap.DAL.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("UserRole")]
    public partial class UserRole
    {
        public long ID { get; set; }

        public long UserId { get; set; }

        [Required]
        [StringLength(2147483647)]
        public string RoleName { get; set; }

        public virtual Role Role { get; set; }

        public virtual User User { get; set; }
    }
}
