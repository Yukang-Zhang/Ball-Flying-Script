using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.XR;
using System.Linq;
using UnityEngine.InputSystem.Android;
using Random = UnityEngine.Random;

public class Ball : MonoBehaviour
{
    private int bezier_i = 0;
    //控制贝塞尔曲线 t 的参数
    
    private Vector3 start;
    private Vector3 p1;
    private Vector3 goal = new Vector3(0f, 0.5f, 2f);
    //这三个点分别是贝塞尔曲线的p0, p1, p2，goal为终点，可以修改

    private Vector3[] diff_p1 = new Vector3[5];
    private float[] diff_total_time = {5f, 3f, 2f, 3f, 3f};
    private int program = 1;

    private bool within_reach = false;
    //判断球距终点是否足够近，使其自然运动

    private float total_dist = 0;
    //统计总移动距离以得到平均速度
    private float real_total_time = 0;

    private Rigidbody rb;
    //刚体组件

    private float dtime;
    //由于Time.deltaTime是波动的，用dt作为固定的帧时间

    private float total_time = 5f;
    //球飞到重点预计的时间，控制运动速度
    private bool grabbed = false;
    //判断球是否处于被抓取状态，若是则自身不进行额外运动
    public string left_or_right = "";

    private bool count_score = false, count_extra_score = false;
    //判断有没有记分

    private int server_num = 0;

    private int score;

    private Vector3 rotation_axis;
    private float rotation_speed;
    
    public void setup(float v, int program, int server_num)
    {
        total_time = v;
        this.program = program;
        this.server_num = server_num;
        start = transform.position;

        int left_or_right = (1 - Random.Range(0, 2) * 2);
        if(this.server_num < 5) {
            if (program == 0) {
                score = 300;
                goal = new Vector3(-transform.position.x / 15f, 0.5f, -transform.position.z / 15f);
            } else if (program == 1) {
                score = 500;
                goal = new Vector3(-transform.position.x / 15f, 1f, -transform.position.z / 15f);
            } else {
                score = 888;
                if(this.server_num == 2)
                    goal = new Vector3(1.5f * left_or_right, 0.5f, 3f);
                else if(this.server_num == 0)
                    goal = new Vector3(1.5f * left_or_right - 1.2f, 0.5f, 3f);
                else if(this.server_num == 1)
                    goal = new Vector3(1.5f * left_or_right - 0.5f, 0.5f, 3f);
                else if(this.server_num == 3)
                    goal = new Vector3(1.5f * left_or_right + 0.5f, 0.5f, 3f);
                else if(this.server_num == 4)
                    goal = new Vector3(1.5f * left_or_right + 1.2f, 0.5f, 3f);
            }
        } else {
            if (program == 0) {
                score = 300;
                goal = new Vector3(-transform.position.x / 20f, 0.5f, -transform.position.z / 20f);
            } else if (program == 1) {
                score = 500;
                goal = new Vector3(-transform.position.x / 20f, 1f, -transform.position.z / 20f);
            } else {
                score = 888;
                if(this.server_num == 7)
                    goal = new Vector3(0.5f * left_or_right, 0.5f, 1.5f);
                else if(this.server_num == 5)
                    goal = new Vector3(0.5f * left_or_right - 1.2f, 0.5f, 1.5f);
                else if(this.server_num == 6)
                    goal = new Vector3(0.5f * left_or_right - 0.5f, 0.5f, 1.5f);
                else if(this.server_num == 8)
                    goal = new Vector3(0.5f * left_or_right + 0.5f, 0.5f, 1.5f);
                else if(this.server_num == 9)
                    goal = new Vector3(0.5f * left_or_right + 1.2f, 0.5f, 1.5f);
            }
        }

        Vector3 random_vector = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)
        ).normalized;
        goal += random_vector * 0.4f;
        
        diff_p1[0] = (start + goal) / 2 + (this.server_num < 5 ? 2f : 0.5f) * Vector3.up * ((start - goal).y > 0 ? (start - goal).y : (goal - start).y); //低抛物线
        diff_p1[1] = (start + goal) / 2; //直线
        diff_p1[2] = (start + goal) / 2 + 0.5f * Vector3.up * ((start - goal).y > 0 ? (start - goal).y : (goal - start).y) + Vector3.left * 5 * left_or_right; //香蕉球
    
        p1 = diff_p1[program];
    }
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if(program == 0)
            AudioManager.playfly();
        else
            AudioManager.playflyfast();
        rotation_axis = Random.onUnitSphere;
        rotation_speed = Random.Range(120f, 240f) * (program + 0.5f) * 3;
    }

    void Update()
    {
        /*
        总时间：T
        帧时间：dt
        一共需要移动T/dt次，取T/dt个点

        ti = i * dt / T, 1 <= i <= T / dt
        取T = 1.0f，球一秒之内移动到玩家处
        */
        
        Vector3 new_pos;
        //球这一帧需要到达的位置
        
        if (bezier_i == 0)
            dtime = Time.deltaTime;
        
        if (!grabbed && !within_reach && bezier_i <= Math.Ceiling(total_time / dtime))
        {
            bezier_i++;
            
            //rb.velocity = (getBezierCurve(bezier_i * Time.deltaTime / 1.0f, start, p1, goal) - transform.position) / Time.deltaTime;
            new_pos = getBezierCurve(bezier_i * dtime / total_time, start, p1, goal);
            total_dist += Vector3.Distance(transform.position, new_pos);
            real_total_time += Time.deltaTime;
            
            if (Math.Abs(transform.position.x) <= 0.1 && Math.Abs(transform.position.z) <= 0.1 || (bezier_i + 1) * dtime / total_time > 1) 
            {
                within_reach = true;
                rb.useGravity = true;
                rb.velocity = (new_pos - transform.position) * total_dist / real_total_time;
            }
            
            transform.position = new_pos;
            transform.Rotate(rotation_axis, rotation_speed * Time.deltaTime);
        }

        if (grabbed)
        {
            Destroy(gameObject, 3f);
            addScore(2, left_or_right);
        }
    }
    
    //计算二阶贝塞尔曲线
    private Vector3 getBezierCurve(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        return (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
    }

    private void OnCollisionEnter(Collision other)
    {
        within_reach = true;
        rb.useGravity = true;
        if (other.gameObject.CompareTag("lefthandcore"))
            addScore(1, "left");
        else if(other.gameObject.CompareTag("righthandcore"))
            addScore(1, "right");
        else
        {
            addScore(0, "neither");
        }
    }

    public void hand_grab()
    {
        grabbed = !grabbed;
    }
    public void addScore(int score, string left_or_right)
    {
        if (score > 0 && !count_score) {
            AudioManager.playcatch();
            AudioManager.playsuccess();
            if (left_or_right == "left")
                GameManager.Instance.play_particle_effect("left");
            else
                GameManager.Instance.play_particle_effect("right");
        } else if(!count_score) {
            AudioManager.playfail();
            count_score = true;
            count_extra_score = true;
        }
        
        if (score == 1 && !count_score) {
            GameManager.Instance.GameData.Score += this.score;
            GameManager.Instance.GameData.TotalScore += this.score;
            count_score = true;
        } else if(score == 2 && !count_score && !count_extra_score) {
            GameManager.Instance.GameData.Score += (int)(this.score * 1.5);
            GameManager.Instance.GameData.TotalScore += (int)(this.score * 1.5);
            count_score = true;
            count_extra_score = true;
        } else if(score == 2 && !count_extra_score) {
            GameManager.Instance.GameData.Score += (int)(this.score * 0.5);
            GameManager.Instance.GameData.TotalScore += (int)(this.score * 0.5);
            count_extra_score = true;
        }
    }
}
